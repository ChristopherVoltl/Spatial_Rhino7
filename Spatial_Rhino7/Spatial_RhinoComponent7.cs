using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using SMT;
using static SMT.SMTUtilities;
using Rhino;
using System.Linq;
using Rhino.Commands;
using System.Security.Cryptography;
using Rhino.UI;
using System.Net;
using static Spatial_Rhino7.Spatial_RhinoComponent7.CurvePlaneDivider;
using Compas.Robot.Link;
using Rhino.DocObjects;
using System.IO;
using System.Collections;
using static Rhino.Render.TextureGraphInfo;
using System.IO.Ports;

namespace Spatial_Rhino7
{
    public class Spatial_RhinoComponent7 : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        /// 
        static SuperMatterToolsPlugin smtPlugin => SuperMatterToolsPlugin.Instance;
        


        public Spatial_RhinoComponent7() : base("SpatialPrintingComponent03", "SPC", "Spatial printing sorting component", "FGAM", "SpatialPrinting")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            //pManager.AddPlaneParameter("pathPlanes ", "pP", " an array of Planes", GH_ParamAccess.list);
            pManager.AddCurveParameter("pathCurves", "pC", " an array of Curves", GH_ParamAccess.list);
            pManager.AddNumberParameter("Parameter", "P", "Parameter to split the curves at (between 0 and 1)", GH_ParamAccess.item, 0.9);
            pManager.AddNumberParameter("Angled Down POC", "AD P", "Point on curve where angled rotation stops", GH_ParamAccess.item, 0.9);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            //pManager.AddCurveParameter("PolyLines", "L", "Output Curves", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Planes", "PL", "Planes created at division points", GH_ParamAccess.list);
            //pManager.AddPointParameter("SuperShape", "SS", "SuperShape for each plane", GH_ParamAccess.list);
        }
        public class Quaternion_interpolation
        {
            //Method to preform the quaternion interpolation for the curves
            //To use in the function based on the curve orientation
            public static List<Plane> interpolation(Curve curve, Plane planeStart, Plane planeEnd, int divisions)
            {
                /// <summary>
                // List to store the interpolated planes
                List<Plane> planes = new List<Plane>();

                // Divide the curve into the specified number of divisions
                double[] curveParams = curve.DivideByCount(divisions - 1, true);

                // Loop through the division points to interpolate between start and end planes
                for (int i = 0; i < curveParams.Length; i++)
                {
                    // Get the point on the curve at the current parameter
                    Point3d pointOnCurve = curve.PointAt(curveParams[i]);
                    // Calculate the interpolation factor (t) between 0 and 1
                    double t = (double)i / (curveParams.Length - 1);

                    // Interpolate the origin (position) of the plane
                    Point3d origin = InterpolatePoint(planeStart.Origin, planeEnd.Origin, t);

                    // Interpolate the X, Y, Z axes of the plane
                    Vector3d xAxis = InterpolateVector(planeStart.XAxis, planeEnd.XAxis, t);
                    Vector3d yAxis = InterpolateVector(planeStart.YAxis, planeEnd.YAxis, t);
                    Vector3d zAxis = InterpolateVector(planeStart.ZAxis, planeEnd.ZAxis, t);

                    // Construct a new plane with the interpolated origin and axes
                    Plane newPlane = new Plane(origin, xAxis, yAxis);
                    
                    // Add the new plane to the list
                    planes.Add(newPlane);
                }

                // Output the interpolated planes
                return planes;
            }
        }

            // Method to interpolate between two points
            public static Point3d InterpolatePoint(Point3d p0, Point3d p1, double t)
            {
                return new Point3d(
                  (1 - t) * p0.X + t * p1.X,
                  (1 - t) * p0.Y + t * p1.Y,
                  (1 - t) * p0.Z + t * p1.Z
                  );
            }

            // Method to interpolate between two vectors
            public static Vector3d InterpolateVector(Vector3d v0, Vector3d v1, double t)
            {
                return new Vector3d(
                  (1 - t) * v0.X + t * v1.X,
                  (1 - t) * v0.Y + t * v1.Y,
                  (1 - t) * v0.Z + t * v1.Z
                  );
            }


            public class CurvePlaneDivider
            {
                /// <summary>
                /// Divides a curve into equal parts and returns the planes at the division points.
                /// </summary>
                // <param name="curve">The curve to divide</param>
                // <param name="numDivisions">The number of divisions (how many planes to create)</param>
                /// <returns>A list of planes created at division points along the curve</returns>
                public static List<Plane> DivideCurveIntoPlanes(Curve curve, int numDivisions)
                {
                    List<Plane> planes = new List<Plane>();

                    if (curve == null || !curve.IsValid)
                        return planes;

                    // Divide the curve into the specified number of parts
                    double[] divisionParams = curve.DivideByCount(numDivisions, true);

                    if (divisionParams == null || divisionParams.Length == 0)
                        return planes;

                    // Create planes at each division point
                    for (int i = 0; i < divisionParams.Length; i++)
                    {
                        // Get the point on the curve at the current parameter
                        Point3d divisionPoint = curve.PointAt(divisionParams[i]);

                        // Create a plane at the division point, with Z-axis normal to the curve's tangent
                        Vector3d zVector = new Vector3d(0, 0, -1);
                        Plane plane = new Plane(divisionPoint, zVector);


                        // Add the plane to the list
                        planes.Add(plane);
                    }

                    return planes;
                }
            }
     

        public class PolylinePlaneDivider
        {
            /// <summary>
            /// Divides a polyline into equal parts based on the specified length and returns planes at the division points.
            /// </summary>
            /// <param name="polyline">The polyline to divide</param>
            /// <param name="divisionLength">The length of each division</param>
            /// <returns>A list of planes created at division points along the polyline</returns>
            public static List<Plane> DividePolylineByLength(Curve polyline, double divisionLength)
            {
                List<Plane> planes = new List<Plane>();

                if (polyline == null || divisionLength <= 0)
                    return planes;

      

                // Divide the polyline curve into equal length segments
                double[] divisionParams = polyline.DivideByLength(divisionLength, true);

                if (divisionParams == null || divisionParams.Length == 0)
                    return planes;

                // Create planes at each division point
                for (int i = 0; i < divisionParams.Length; i++)
                {
                    Point3d divisionPoint = polyline.PointAt(divisionParams[i]);


                    Vector3d ZAxis = new Vector3d(0, 0, -1);
                    // Create a plane at the division point with the tangent as an axis
                    Plane plane = new Plane(divisionPoint, ZAxis);

                    // Add the plane to the list
                    planes.Add(plane);
                }

                return planes;
            }
        }

        public class LineCheck
        {
            public static List<Curve> ExplodeCurves(Curve curve)
            {
                List<Curve> curves = new List<Curve>();

                if (curve == null || !curve.IsValid)
                    return curves;

                // Explode the curve into segments
                Curve[] segments = curve.DuplicateSegments();

                if (segments == null || segments.Length == 0)
                    return curves;

                // Add the segments to the list
                curves.AddRange(segments);

                return curves;
            }
            public static Boolean unsupporedCheck(List<Curve> curves, Curve currentCrv)
            {
                Boolean check = true;

                if (curves.Count == 0)
                {
                    check = true;
                }
                else
                {
                    for (int i = 0; i < curves.Count; i++)
                    {
                        if (curves[i].PointAtEnd == currentCrv.PointAtEnd)
                        {
                            check = false;
                            break;
                        }
                    }
                }

                return check;
            }

            public static int connectivityNodeCount(List<Point> points, Curve currentCrv)
                {
                int count = 0;

                if (points.Count == 0)
                {
                    count = 0;
                }
                else
                {
                    for (int i = 0; i < points.Count; i++)
                    {
                        //if the currentCrv end point is equal to the point in the list add 1 to the count
                        if (points[i].Location == currentCrv.PointAtEnd)
                        {
                            count++;
                        }
                    }
                }

                return count;
            }
            

            public static String OrientLine(Curve curve)
            {
                double tolerance = 1.0;
                ///Horizontal Line
                if (Math.Abs(curve.PointAtStart.Z - curve.PointAtEnd.Z) <= tolerance)
                {
                    string lineDescriptor = "Horizontal";
                    return lineDescriptor;
                }

                ///Vertial Line
                else if (curve.PointAtStart.Z < curve.PointAtEnd.Z & curve.PointAtStart.X == curve.PointAtEnd.X & curve.PointAtStart.Y == curve.PointAtEnd.Y)
                {
                    string lineDescriptor = "Vertical";
                    return lineDescriptor;
                }

                ///Angled Up Line
                else if (curve.PointAtStart.Z < curve.PointAtEnd.Z)
                {
                    string lineDescriptor = "AngledUp";
                    return lineDescriptor;
                }

                ///Angled Down Line
                else if (curve.PointAtStart.Z > curve.PointAtEnd.Z)
                {
                    string lineDescriptor = "AngledDown";
                    return lineDescriptor;
                }

                else
                {
                    return null;
                }
            }
        }


/// </summary>
/// 

protected override void SolveInstance(IGH_DataAccess DA)
        {
            RhinoDoc doc = Rhino.RhinoDoc.ActiveDoc;
            // 1. Declare placeholder variables and assign initial invalid data.
            //    This way, if the input parameters fail to supply valid data, we know when to abort.
            List<Plane> pathPlanes = new List<Plane>();
            List<Curve> pathCurves = new List<Curve>();
            double parameter = 0.5; // Default parameter


            // 2. Retrieve input data.
            //if (!DA.GetDataList(0, pathPlanes)) { return; }
            if (!DA.GetDataList(0, pathCurves)) { return; }
            if (!DA.GetData(1, ref parameter)) return;

            // 3. Abort on invalid inputs.
            if (pathCurves == null)
            {
                RhinoApp.WriteLine("The selected object is not a curve.");
            }

    
            List<Point3d> pData_points = new List<Point3d>();
            List<Plane> allPlanes = new List<Plane>();

            //get the operation UI!
            int progIndex = smtPlugin.UIData.ProgramIndex;
            int opIndex = smtPlugin.UIData.OperationIndex;
            if (progIndex > -1 && opIndex > -1)
            {
                OperationUI opUI = smtPlugin.UIData.TreeRootUI.WC.ChildNodes[progIndex].ChildNodes[opIndex];
                if (opUI != null)
                {

                    opUI.DivStyle = DivisionStyle.PointData;
                    opUI.FeedMode = FeedMapping.PointData;
                    opUI.ZOrientationStyle = ZOrientStyle.PointData;
                    opUI.YOrientationStyle = YOrientStyle.PointData;
                    opUI.LIStyle = InOutStyle.Inactive;
                    opUI.LOStyle = InOutStyle.Inactive;
                    //opUI.ApproxDist = 0.0f;
                    opUI.PTP_Traverse = true;


                    //actionstates of the extrusion operation
                    ActionState extrudeAct = opUI.SuperOperationRef.GetActionState("Extrude");
                    SuperActionUI actionUI = opUI.ActionControls["Extrude"];
                    actionUI.ActivationMode = ActivationStyle.PointData;

                    ActionState nozzleCoolingAct = opUI.SuperOperationRef.GetActionState("NozzleCooling");
                    SuperActionUI actionCoolingUI = opUI.ActionControls["NozzleCooling"];
                    actionCoolingUI.ActivationMode = ActivationStyle.PointData;

                    ActionState PauseAct = opUI.SuperOperationRef.GetActionState("CycleWait");
                    SuperActionUI actionPauseUI = opUI.ActionControls["CycleWait"];
                    actionPauseUI.StartValue = "6.0";
                    actionPauseUI.ActivationMode = ActivationStyle.PointData;
                    //extrude actionstates
                    SuperEvent extrude = new SuperEvent(extrudeAct, 0.0, EventType.Activate, true);
                    SuperEvent stopExtrude = new SuperEvent(extrudeAct, 0.0, EventType.Deactivate, true);
                    //fan actionstates
                    SuperEvent cool = new SuperEvent(nozzleCoolingAct, 0.0, EventType.Activate, true);
                    SuperEvent stopCooling = new SuperEvent(nozzleCoolingAct, 0.0, EventType.Deactivate, true);
                    //nozzle cooling actionstates
                    SuperEvent cycleWait = new SuperEvent(PauseAct, 0.0, EventType.Activate, true);
                    SuperEvent stopcycleWait = new SuperEvent(PauseAct, 0.0, EventType.Deactivate, true);

   
                    //AxisState axisStateE5 = new AxisState();
                    //Extrusion Values
                    double extrusionValue_vertical = 2.4;
                    double extrusionValue_horizontal = 1.0;
                    double extrusionValue_angled = 2.0;


                    //given an array of ordered and oriented planes for each spatial extrusion location
                    //build paths

                    //input curve and slicing parameters

                    SuperShape[] shapes = new SuperShape[1];

                    //Assinging SMT functions based in the length of the curve
                    List<SMTPData> pDataList = new List<SMTPData>();
                    List<Curve> printedPath = new List<Curve>();

                    SMTPData[] pData = new SMTPData[11];
                    int counter = 0;

                    //Keep track of start and end points that have been used in a list to create 
                    //different path methosd for extrusion 


                    //loop through each path line or polyline 
                    for (int j = 0; j < pathCurves.Count; j++)
                    {
                        if (pathCurves[j] == null || !pathCurves[j].IsValid) continue;

                        //get the planes for each path
                        //List<Plane> crvPathPlanes = PolylinePlaneDivider.DividePolylineByLength(pathCurves[j], 10);                     

                       // List<Curve> segments = LineCheck.ExplodeCurves(pathCurves[j]);

                        //this will be used to make all the curves AnglesUp 

                        /*if (pathCurves[j].PointAtStart.Z > pathCurves[j].PointAtEnd.Z)
                        {
                            pathCurves[j].Reverse();
                        }*/

                        //get the planes for each path
                        int numCrvPathPlanes = 10;
                        List<Plane> crvPathPlanes = DivideCurveIntoPlanes(pathCurves[j], numCrvPathPlanes);

                        //for each point, start extrusion, extrude path, end extrusion.

                        //for each curve define if the curve is angled up, down, horizontal or vertical
                        string lineDescriptor = LineCheck.OrientLine(pathCurves[j]);
                        

                            

                        //if the curve is angled up
                        if (lineDescriptor == "AngledUp")
                        {   
                            //function to determine if the curve is unsupported
                            Boolean isUnsupported = LineCheck.unsupporedCheck(printedPath,pathCurves[j]);

                            //If the curve is unsupported do this
                            if (isUnsupported)
                            {
                                //nothing will change
                               

                                //get the first and last plane of the curve
                                Plane pathStart = crvPathPlanes[0];
                                Plane pathEnd = crvPathPlanes[crvPathPlanes.Count - 1];
                                double t = 0.5;
                                Curve curve =pathCurves[j];
                                curve.Domain = new Interval(0, 1);
                                Point3d pointOnCurve = curve.PointAt(t);
                                Vector3d tangent = curve.TangentAt(t);
                                Vector3d zAxis = pathStart.Origin - pathEnd.Origin;

                                //stop extruding before the end of the curve to reduce leakage
                                double crv_Length = curve.GetLength();
                                double stopExtruding = crv_Length * 0.85;

                                //start cooling at 10 mm
                                double startCooling = crv_Length * 0.06;

                                //Get the parameter to start the circle motion 2mm above pathstart
                                double startExtruding = 2.5;

                                //get start extruding point 2mm above the start of the curve in the Z direction
                                Point3d startExtruding_pt = new Point3d(pathStart.Origin.X, pathStart.Origin.Y, pathStart.Origin.Z + startExtruding);
                                //get the parameter of the curve at the startExtruding length
                                //curve.LengthParameter(startExtruding, out double startExtrudeParam);
                                //Point3d startExtruding_pt = curve.PointAt(startExtrudeParam);
                                
                                //new curve from startExtruding_pt to pathEnd
                                Curve pathModified = new LineCurve(startExtruding_pt, pathEnd.Origin);


                                //get the parameter of the curve at the startCooling length
                                pathModified.LengthParameter(startCooling, out double startCoolingParam);
                                Point3d startCooling_pt = pathModified.PointAt(startCoolingParam);

                                //get the parameter of the curve at the stopExtruding length
                                pathModified.LengthParameter(stopExtruding, out double stopExtrudeParam);
                                Point3d stopExtruding_pt = pathModified.PointAt(stopExtrudeParam);

                                

                                zAxis.Unitize();

                                // Step 2: Calculate the angle between Z-axis and world Z-axis in degrees
                                Point3d projectedEndPt = new Point3d(pathEnd.Origin.X, pathEnd.Origin.Y, pathStart.Origin.Z);
                                Vector3d crvVec = pathEnd.Origin - pathStart.Origin;
                                Vector3d projectedCrvVec = projectedEndPt - pathStart.Origin;

                                double angleToWorldZ = Vector3d.VectorAngle(crvVec, projectedCrvVec) * (180.0 / Math.PI);

                                //If the angle is less than 45 degrees, adjust Z-axis to be capped at 45 degrees
                                if (angleToWorldZ < 75.0)
                                {
                                    // Rotate zAxis to be at a 45-degree angle to the world Z-axis
                                    double rotationAngle =  angleToWorldZ - 115;  // Calculate the amount to rotate
                                    Transform rotation = Transform.Rotation(rotationAngle * (Math.PI / 180.0), Vector3d.CrossProduct(zAxis, Vector3d.ZAxis), pathStart.Origin);
                                    zAxis.Transform(rotation);
                                }

                                //Calculate the initial X-axis to be perpendicular to Z and aligned to the "left" of the curve direction
                                Vector3d xAxis = Vector3d.CrossProduct(Vector3d.ZAxis, zAxis);
                                xAxis.Unitize();

                                // If the X-axis is zero (e.g., if Z-axis is vertical), use the world Y-axis as a fallback
                                if (xAxis.IsZero)
                                {
                                    xAxis = Vector3d.CrossProduct(Vector3d.YAxis, zAxis);
                                    xAxis.Unitize();
                                }

                                //Rotate X-axis by 180 degrees around Z-axis by negating it
                                double xAxisDif = Vector3d.VectorAngle(xAxis, Vector3d.XAxis) * (180.0 / Math.PI);

                                if (Math.Abs(xAxisDif) < 90)
                                {
                                    xAxis = -xAxis;
                                }

                                //Calculate the Y-axis to complete the orthogonal system
                                Vector3d yAxis = Vector3d.CrossProduct(zAxis, xAxis);
                                yAxis.Unitize();


                                //Construct the plane with the calculated axes
                                Plane plane = new Plane(pointOnCurve, xAxis, yAxis);
                                Plane planeAtEnd = new Plane(pathEnd.Origin, xAxis, yAxis);
                                Plane planeAtStart = new Plane(pathStart.Origin, xAxis, yAxis);
                                Plane startExtrudingPlane = new Plane(startExtruding_pt, xAxis, yAxis);
                                Plane stopExtrudingPlane = new Plane(stopExtruding_pt, xAxis, yAxis);
                                Plane startCooling_plane = new Plane(startCooling_pt, xAxis, yAxis);




                                //Get the plane orientation of the curve based on the start and end point
                                List<Plane> planeInterpolation = Quaternion_interpolation.interpolation(pathCurves[j], planeAtStart, planeAtEnd, numCrvPathPlanes);


                                //create the extrusion data

                                //define if the extrusion needs a traversal path by calculating the distance between the last curve end and the current curve start
                                //if the distance is greater than 10mm, add a traversal path
                                try
                                {
                                    Curve PrevPath = pathCurves[j - 1];
                                    Point3d PrevPathStart = PrevPath.PointAtStart;
                                    Plane TraversalPathEndPlaneStart = new Plane(PrevPathStart, xAxis, yAxis);
                                    Point3d PrevPathEnd = PrevPath.PointAtEnd;
                                    Plane TraversalPlanePlaneEnd = new Plane(PrevPathEnd, -Vector3d.XAxis, Vector3d.YAxis);


                                    //if the distance between the end of the previous curve and the start of the
                                    //current curve is greater than 10mm, add a traversal path
                                    if (PrevPathEnd.DistanceTo(pathStart.Origin) > 10)
                                    {

                                        pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPlanePlaneEnd, stopExtrude, 3.0f);
                                        pData[0].AxisValues["E5"] = 1.0;
                                        pData[0].Events["NozzleCooling"] = stopCooling;
                                        allPlanes.Add(TraversalPlanePlaneEnd);
                                        pDataList.Add(pData[0]);
                                        counter++;

                                        //create traversal path
                                        //move end point 10mm vertically
                                        Point3d TraversalPath = new Point3d(PrevPathEnd.X, PrevPathEnd.Y, PrevPathEnd.Z + 30);
                                        Plane TraversalPlane = new Plane(TraversalPath, -Vector3d.XAxis, Vector3d.YAxis);
                                        pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPlane, 2.0f);
                                        pData[1].AxisValues["E5"] = 1.0;
                                        allPlanes.Add(TraversalPlane);
                                        pDataList.Add(pData[1]);
                                        counter++;

                                        //move to loction above the current curve at the same Z height of TraversalPath
                                        Point3d TraversalPathEnd = new Point3d(pathStart.Origin.X, pathStart.Origin.Y, TraversalPath.Z);
                                        Plane TraversalPathEndPlane = new Plane(TraversalPathEnd, -Vector3d.XAxis, Vector3d.YAxis);
                                        pData[2] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPathEndPlane, stopExtrude, 3.0f);
                                        pData[2].AxisValues["E5"] = 1.0;
                                        allPlanes.Add(TraversalPathEndPlane);
                                        pDataList.Add(pData[2]);
                                        counter++;
                                    }
                                    else
                                    {
                                        pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, startExtrudingPlane, stopExtrude, 3.0f);
                                        pData[0].AxisValues["E5"] = 1.0;

                                        pDataList.Add(pData[0]);
                                        counter++;

                                        pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, startExtrudingPlane, stopCooling, 3.0f);
                                        pData[1].AxisValues["E5"] = 1.0;
                                        pDataList.Add(pData[1]);
                                        counter++;
                                    }
                                }
                                catch (ArgumentOutOfRangeException)
                                {
                                    pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, startExtrudingPlane, stopExtrude, 3.0f);
                                    pData[0].AxisValues["E5"] = 1.0;
                                    pData[0].Events["NozzleCooling"] = stopCooling;
                                    pDataList.Add(pData[0]);
                                    counter++;

                                }
                                

                                //define the circle motion for the start of the extrusion
                                Circle circle = new Circle(startExtrudingPlane, 1);

                                //start origin of extrusion path
                                pData[3] = new SMTPData(counter, 0, 0, MoveType.Lin, startExtrudingPlane, 3.0f);
                                pData[3].AxisValues["E5"] = 2.4;
                                pDataList.Add(pData[3]);
                                counter++;

                                //doc.Objects.AddCircle(circle);
                                //doc.Views.Redraw();
                                int numPlanes = 10;
                                List<Plane> circlePathPlanes = DivideCurveIntoPlanes(circle.ToNurbsCurve(), numPlanes);
                                //loop through the circle planes
                                for (int k = 0; k < circlePathPlanes.Count; k++)
                                {
                                    Plane cirPath = circlePathPlanes[k];
                                    
                                    pData[4] = new SMTPData(counter, 0, 0, MoveType.Lin, cirPath, 0.1f);
                                    //pData[4].AxisValues["E5"] = 2.4;
                                    pData[4].Events["Extrude"] = extrude;
                                    

                                    pDataList.Add(pData[4]);
                                    allPlanes.Add(cirPath);
                                    counter++;
                                }
                                pData[5] = new SMTPData(counter, 0, 0, MoveType.Lin, startExtrudingPlane, 0.5f);
                                //pData[5].Events["NozzleCooling"] = cool;
                                
                                pData[5].AxisValues["E5"] = 2.4;
                                pData[5].Events["Extrude"] = extrude;
                                //allPlanes.Add(pathStart);
                                pDataList.Add(pData[5]);
                                counter++;
                                

                                pData[6] = new SMTPData(counter, 0, 0, MoveType.Lin, startCooling_plane, 0.075f);
                                pData[6].Events["NozzleCooling"] = cool;
                                pData[6].AxisValues["E5"] = 2.4;
                                pData[6].Events["Extrude"] = extrude;

                                pDataList.Add(pData[6]);
                                allPlanes.Add(startCooling_plane);
                                counter++;
                              

                                pData[7] = new SMTPData(counter, 0, 0, MoveType.Lin, stopExtrudingPlane, 0.075f);
                                pData[7].AxisValues["E5"] = 2.4;
                                pData[7].Events["Extrude"] = stopExtrude;
                                pDataList.Add(pData[7]);
                                allPlanes.Add(stopExtrudingPlane);
                                counter++;

                                //doc.Objects.AddCircle(circle);
                                //doc.Views.Redraw();
                                pData[8] = new SMTPData(counter, 0, 0, MoveType.Lin, stopExtrudingPlane, 0.075f);
                                //pData[7].Events["Extrude"] = stopExtrude;
                                pData[8].Events["CycleWait"] = cycleWait;

                                //allPlanes.Add(pathEnd);
                                pDataList.Add(pData[8]);
                                counter++;

                                pData[9] = new SMTPData(counter, 0, 0, MoveType.Lin, stopExtrudingPlane, 0.075f);
                                pData[9].AxisValues["E5"] = 2.4;
                                pData[9].Events["Extrude"] = extrude;



                                //allPlanes.Add(pathEnd);
                                pDataList.Add(pData[9]);
                                counter++;

                                pData[10] = new SMTPData(counter, 0, 0, MoveType.Lin, pathEnd, 0.075f);
                                pData[10].AxisValues["E5"] = 2.4;
                                pData[10].Events["Extrude"] = stopExtrude;

                                //allPlanes.Add(pathEnd);
                                pDataList.Add(pData[10]);
                                counter++;




                            }
                            else
                            {
                                //path modification to account for intersection with the unsupported curve

                                    

                                double t = 0.5;
                                Curve curve =pathCurves[j];
                                curve.Domain = new Interval(0, 1);
                                Point3d pointOnCurve = curve.PointAt(t);
                                Vector3d tangent = curve.TangentAt(t);
                                    

                                //path modification
                                Point3d crvStart = curve.PointAtStart;
                                Point3d crvEnd = curve.PointAtEnd;

                                //stop extruding before the end of the curve to reduce leakage
                                double crv_Length = curve.GetLength();
                                double stopExtruding = crv_Length * 0.85;

                                //start cooling at 10 mm
                                double startCooling = crv_Length * 0.06;

                                //Get the parameter to start the circle motion 2mm above pathstart
                                double startExtruding = 2.0;

                                //get the parameter of the curve at the startExtruding length
                                curve.LengthParameter(startExtruding, out double startExtrudeParam);
                                Point3d startExtruding_pt = curve.PointAt(startExtrudeParam);

                                //get the parameter of the curve at the startCooling length
                                curve.LengthParameter(startCooling, out double startCoolingParam);
                                Point3d startCooling_pt = curve.PointAt(startCoolingParam);

                                //get the parameter of the curve at the stopExtruding length
                                curve.LengthParameter(stopExtruding, out double stopExtrudeParam);
                                Point3d stopExtruding_pt = curve.PointAt(stopExtrudeParam);

                                // Convert degrees to radians
                                double angleRadians = RhinoMath.ToRadians(15);

                                // Create a rotation transformation
                                Transform crvRotation = Transform.Rotation(angleRadians, Vector3d.ZAxis, crvStart);
                                Curve rotatedCrv = curve.DuplicateCurve();
                                rotatedCrv.Transform(crvRotation);
                                Point3d newEnd = rotatedCrv.PointAtEnd;
                                Curve pathModified = new LineCurve(crvStart, newEnd);
                                    
                                //get the planes for each path
                                int numPathModifiedPlanes = 10;
                                    
                                List<Plane> pathModifiedPlanes = DivideCurveIntoPlanes(pathModified, numPathModifiedPlanes);
                                //get the first and last plane of the curve
                                Plane pathStart = pathModifiedPlanes[0];
                                Plane pathEnd = pathModifiedPlanes[pathModifiedPlanes.Count - 1];
                                Vector3d zAxis = pathStart.Origin - pathEnd.Origin;


                                zAxis.Unitize();

                                // Step 2: Calculate the angle between Z-axis and world Z-axis in degrees
                                Point3d projectedEndPt = new Point3d(pathEnd.Origin.X, pathEnd.Origin.Y, pathStart.Origin.Z);
                                Vector3d crvVec = pathEnd.Origin - pathStart.Origin;
                                Vector3d projectedCrvVec = projectedEndPt - pathStart.Origin;

                                double angleToWorldZ = Vector3d.VectorAngle(crvVec, projectedCrvVec) * (180.0 / Math.PI);

                                //If the angle is less than 45 degrees, adjust Z-axis to be capped at 45 degrees
                                if (angleToWorldZ < 45.0)
                                {
                                    // Rotate zAxis to be at a 45-degree angle to the world Z-axis
                                    double rotationAngle = angleToWorldZ - 55;  // Calculate the amount to rotate
                                    Transform rotation = Transform.Rotation(rotationAngle * (Math.PI / 180.0), Vector3d.CrossProduct(zAxis, Vector3d.ZAxis), pathStart.Origin);
                                    zAxis.Transform(rotation);
                                }

                                //Calculate the initial X-axis to be perpendicular to Z and aligned to the "left" of the curve direction
                                Vector3d xAxis = Vector3d.CrossProduct(Vector3d.ZAxis, zAxis);
                                xAxis.Unitize();

                                // If the X-axis is zero (e.g., if Z-axis is vertical), use the world Y-axis as a fallback
                                if (xAxis.IsZero)
                                {
                                    xAxis = Vector3d.CrossProduct(Vector3d.YAxis, zAxis);
                                    xAxis.Unitize();
                                }

                                //Rotate X-axis by 180 degrees around Z-axis by negating it
                                double xAxisDif = Vector3d.VectorAngle(xAxis, Vector3d.XAxis) * (180.0 / Math.PI);

                                if (Math.Abs(xAxisDif) < 90)
                                {
                                    xAxis = -xAxis;
                                }

                                //Calculate the Y-axis to complete the orthogonal system
                                Vector3d yAxis = Vector3d.CrossProduct(zAxis, xAxis);
                                yAxis.Unitize();


                                //Construct the plane with the calculated axes
                                Plane planeAtStart = new Plane(pathStart.Origin, xAxis, yAxis);
                                Plane planeAtEnd = new Plane(pathEnd.Origin, xAxis, yAxis);
                                Plane crvEndPlane = new Plane(crvEnd, xAxis, yAxis);
                                Plane startExtrudingPlane = new Plane(startExtruding_pt, xAxis, yAxis);
                                Plane stopExtrudingPlane = new Plane(stopExtruding_pt, xAxis, yAxis);
                                Plane startCooling_plane = new Plane(startCooling_pt, xAxis, yAxis);    

                                //move planeAtEnd up 14mm
                                Point3d pointAtEndUp = new Point3d(pathEnd.Origin.X, pathEnd.Origin.Y, pathEnd.Origin.Z + 14);
                                Plane planeAtEndUp = new Plane(pointAtEndUp, xAxis, yAxis);

                                //Get the plane orientation of the curve based on the start and end point
                                List<Plane> planeInterpolation = Quaternion_interpolation.interpolation(pathModified, planeAtStart, planeAtEndUp, numCrvPathPlanes);


                                //create the extrusion data

                                //define if the extrusion needs a traversal path by calculating the distance between the last curve end and the current curve start
                                //if the distance is greater than 10mm, add a traversal path


                                try
                                {
                                    Curve PrevPath = pathCurves[j - 1];
                                    Point3d PrevPathStart = PrevPath.PointAtStart;
                                    Plane TraversalPathEndPlaneStart = new Plane(PrevPathStart, xAxis, yAxis);
                                    Point3d PrevPathEnd = PrevPath.PointAtEnd;
                                    Plane TraversalPlanePlaneEnd = new Plane(PrevPathEnd, -Vector3d.XAxis, Vector3d.YAxis);


                                    //if the distance between the end of the previous curve and the start of the
                                    //current curve is greater than 10mm, add a traversal path
                                    if (PrevPathEnd.DistanceTo(pathStart.Origin) > 10)
                                    {

                                        pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPlanePlaneEnd, stopExtrude, 3.0f);
                                        pData[0].AxisValues["E5"] = 1.0;
                                        pData[0].Events["NozzleCooling"] = stopCooling;
                                        allPlanes.Add(TraversalPlanePlaneEnd);
                                        pDataList.Add(pData[0]);
                                        counter++;

                                        //create traversal path
                                        //move end point 10mm vertically
                                        Point3d TraversalPath = new Point3d(PrevPathEnd.X, PrevPathEnd.Y, PrevPathEnd.Z + 30);
                                        Plane TraversalPlane = new Plane(TraversalPath, -Vector3d.XAxis, Vector3d.YAxis);
                                        pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPlane, 3.0f);
                                        allPlanes.Add(TraversalPlane);
                                        pDataList.Add(pData[1]);
                                        counter++;

                                        //move to loction above the current curve at the same Z height of TraversalPath
                                        Point3d TraversalPathEnd = new Point3d(pathStart.Origin.X, pathStart.Origin.Y, TraversalPath.Z);
                                        Plane TraversalPathEndPlane = new Plane(TraversalPathEnd, -Vector3d.XAxis, Vector3d.YAxis);
                                        pData[2] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPathEndPlane, stopExtrude, 3.0f);
                                        allPlanes.Add(TraversalPathEndPlane);
                                        pDataList.Add(pData[2]);
                                        counter++;
                                    }
                                    else
                                    {
                                        pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, stopExtrude, 3.0f);
                                        pData[0].AxisValues["E5"] = 1.0;

                                        pDataList.Add(pData[0]);
                                        counter++;

                                        pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, stopCooling, 3.0f);
                                        pDataList.Add(pData[1]);
                                        counter++;
                                    }
                                }
                                catch (ArgumentOutOfRangeException)
                                {
                                    pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, stopExtrude, 3.0f);
                                    pData[0].AxisValues["E5"] = 1.0;
                                    pData[0].Events["NozzleCooling"] = stopCooling;
                                    pDataList.Add(pData[0]);
                                    counter++;

                                }
                                //define the circle motion for the start of the extrusion
                                Circle circle = new Circle(startExtrudingPlane, 1);

                                //start origin of extrusion path
                                pData[3] = new SMTPData(counter, 0, 0, MoveType.Lin, startExtrudingPlane, 3.0f);
                                pData[3].AxisValues["E5"] = 2.4;
                                pDataList.Add(pData[3]);
                                counter++;

                                //doc.Objects.AddCircle(circle);
                                //doc.Views.Redraw();
                                int numPlanes = 10;
                                List<Plane> circlePathPlanes = DivideCurveIntoPlanes(circle.ToNurbsCurve(), numPlanes);
                                //loop through the circle planes
                                for (int k = 0; k < circlePathPlanes.Count; k++)
                                {
                                    Plane cirPath = circlePathPlanes[k];

                                    pData[4] = new SMTPData(counter, 0, 0, MoveType.Lin, cirPath, 0.1f);
                                    pData[4].AxisValues["E5"] = 2.4;
                                    pData[4].Events["Extrude"] = extrude;


                                    pDataList.Add(pData[4]);
                                    allPlanes.Add(cirPath);
                                    counter++;
                                }
                                pData[5] = new SMTPData(counter, 0, 0, MoveType.Lin, startExtrudingPlane, 0.5f);
                                //pData[5].Events["NozzleCooling"] = cool;
                                pData[5].Events["Extrude"] = extrude;
                                pData[5].AxisValues["E5"] = 2.4;
                                //allPlanes.Add(pathStart);
                                pDataList.Add(pData[5]);
                                counter++;


                                pData[6] = new SMTPData(counter, 0, 0, MoveType.Lin, startCooling_plane, 0.075f);
                                pData[6].Events["NozzleCooling"] = cool;

                                pDataList.Add(pData[6]);
                                allPlanes.Add(startCooling_plane);
                                counter++;


                                pData[7] = new SMTPData(counter, 0, 0, MoveType.Lin, stopExtrudingPlane, 0.075f);
                                pData[7].Events["Extrude"] = stopExtrude;
                                pDataList.Add(pData[7]);
                                allPlanes.Add(stopExtrudingPlane);
                                counter++;

                                //doc.Objects.AddCircle(circle);
                                //doc.Views.Redraw();
                                pData[8] = new SMTPData(counter, 0, 0, MoveType.Lin, stopExtrudingPlane, 0.075f);
                                //pData[7].Events["Extrude"] = stopExtrude;
                                pData[8].Events["CycleWait"] = cycleWait;

                                //allPlanes.Add(pathEnd);
                                pDataList.Add(pData[8]);
                                counter++;

                                pData[9] = new SMTPData(counter, 0, 0, MoveType.Lin, stopExtrudingPlane, 0.075f);
                                pData[9].Events["Extrude"] = extrude;


                                //allPlanes.Add(pathEnd);
                                pDataList.Add(pData[9]);
                                counter++;

                                pData[10] = new SMTPData(counter, 0, 0, MoveType.Lin, pathEnd, 0.075f);
                                pData[10].Events["Extrude"] = stopExtrude;

                                //allPlanes.Add(pathEnd);
                                pDataList.Add(pData[10]);
                                counter++;

                            }
                        }

                        if (lineDescriptor == "AngledDown")
                        {
                            //get the first and last plane of the curve
                            Plane pathStart = crvPathPlanes[0];
                            Plane pathEnd = crvPathPlanes[crvPathPlanes.Count - 1];
                            Vector3d pathVector = pathStart.Origin - pathEnd.Origin;
                            pathVector.Reverse();
                            double angle = RhinoMath.ToRadians(-5);
                            pathVector.Rotate(angle, pathEnd.XAxis);

                            double t = 0.5;
                            Curve curve =pathCurves[j];
                            curve.Domain = new Interval(0, 1);
                            Point3d pointOnCurve = curve.PointAt(t);
                            Vector3d yAxis = curve.TangentAt(t);

                            yAxis.Unitize();

                            // Step 2: Calculate the angle between Z-axis and world Z-axis in degrees
                            Point3d projectedEndPt = new Point3d(pathStart.Origin.X, pathStart.Origin.Y, pathEnd.Origin.Z); 
                            Vector3d crvVec = pathStart.Origin - pathEnd.Origin; // Vector from end to start
                            Vector3d projectedCrvVec = projectedEndPt - pathEnd.Origin;

                            double angleToWorldZ = Vector3d.VectorAngle(crvVec, projectedCrvVec); // Angle between the curve and the projected curve
                            angleToWorldZ = RhinoMath.ToDegrees(angleToWorldZ);
                            //If the angle is less than 45 degrees, adjust Z-axis to be capped at 45 degrees
                            if (angleToWorldZ > 45.0)
                            {
                                // Rotate zAxis to be at a 45-degree angle to the world Z-axis
                                double rotationAngle = angleToWorldZ - 35;  // Calculate the amount to rotate
                                Transform rotation = Transform.Rotation(rotationAngle * (Math.PI / 180.0), Vector3d.CrossProduct(yAxis, Vector3d.ZAxis), pathStart.Origin);
                                yAxis.Transform(rotation);
                            }

                            //Calculate the initial X-axis to be perpendicular to Z and aligned to the "left" of the curve direction
                            Vector3d xAxis = Vector3d.CrossProduct(Vector3d.ZAxis, yAxis);
                            xAxis.Unitize();

                            // If the X-axis is zero (e.g., if Z-axis is vertical), use the world Y-axis as a fallback
                            if (xAxis.IsZero)
                            {
                                xAxis = Vector3d.CrossProduct(Vector3d.YAxis, yAxis);
                                xAxis.Unitize();
                            }

                            //Rotate X-axis by 180 degrees around Z-axis by negating it
                            double xAxisDif = Vector3d.VectorAngle(xAxis, Vector3d.XAxis) * (180.0 / Math.PI);

                            if (Math.Abs(xAxisDif) < 90)
                            {
                                xAxis = -xAxis;
                                yAxis = -yAxis;
                            }

                            //Calculate the Y-axis to complete the orthogonal system
                            Vector3d zAxis = Vector3d.CrossProduct(yAxis, xAxis);
                            zAxis.Unitize();

                            //Construct the plane with the calculated axes
                            Plane plane = new Plane(pointOnCurve, xAxis, yAxis);
                            Plane planeAtStart = new Plane(pathStart.Origin, xAxis, yAxis);
                            Plane planeAtEnd = new Plane(pathEnd.Origin, xAxis, yAxis);

                            //Get the plane orientation of the curve based on the start and end point
                            List<Plane> planeInterpolation = Quaternion_interpolation.interpolation(pathCurves[j], planeAtStart, planeAtEnd, numCrvPathPlanes);

                            //define if the extrusion needs a traversal path by calculating the distance between the last curve end and the current curve start
                            //if the distance is greater than 10mm, add a traversal path
                            try
                            {
                                Curve PrevPath = pathCurves[j - 1];
                                Point3d PrevPathStart = PrevPath.PointAtStart;
                                Plane TraversalPathEndPlaneStart = new Plane(PrevPathStart, xAxis, yAxis);
                                Point3d PrevPathEnd = PrevPath.PointAtEnd;
                                Plane TraversalPlanePlaneEnd = new Plane(PrevPathEnd, -Vector3d.XAxis, Vector3d.YAxis);


                                //if the distance between the end of the previous curve and the start of the
                                //current curve is greater than 10mm, add a traversal path
                                if (PrevPathEnd.DistanceTo(pathStart.Origin) > 10)
                                {

                                    pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPlanePlaneEnd, stopExtrude, 3.0f);
                                    pData[0].AxisValues["E5"] = 1.0;
                                    pData[0].Events["NozzleCooling"] = stopCooling;
                                    allPlanes.Add(TraversalPlanePlaneEnd);
                                    pDataList.Add(pData[0]);
                                    counter++;

                                    //create traversal path
                                    //move end point 10mm vertically
                                    Point3d TraversalPath = new Point3d(PrevPathEnd.X, PrevPathEnd.Y, PrevPathEnd.Z + 20);
                                    Plane TraversalPlane = new Plane(TraversalPath, -Vector3d.XAxis, Vector3d.YAxis);
                                    pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPlane, 2.0f);
                                    pData[1].AxisValues["E5"] = 1.0;
                                    allPlanes.Add(TraversalPlane);
                                    pDataList.Add(pData[1]);
                                    counter++;

                                    //move to loction above the current curve at the same Z height of TraversalPath
                                    Point3d TraversalPathEnd = new Point3d(pathStart.Origin.X, pathStart.Origin.Y, TraversalPath.Z);
                                    Plane TraversalPathEndPlane = new Plane(TraversalPathEnd, -Vector3d.XAxis, Vector3d.YAxis);
                                    pData[2] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPathEndPlane, stopExtrude, 3.0f);
                                    pData[2].AxisValues["E5"] = 1.0;
                                    allPlanes.Add(TraversalPathEndPlane);
                                    pDataList.Add(pData[2]);
                                    counter++;
                                }
                                else
                                {
                                    pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, stopExtrude, 3.0f);
                                    pData[0].AxisValues["E5"] = 1.0;

                                    pDataList.Add(pData[0]);
                                    counter++;

                                    pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, stopCooling, 3.0f);
                                    pData[1].AxisValues["E5"] = 1.0;
                                    pDataList.Add(pData[1]);
                                    counter++;
                                }
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, stopExtrude, 3.0f);
                                pData[0].AxisValues["E5"] = 1.0;
                                pData[0].Events["NozzleCooling"] = stopCooling;
                                pDataList.Add(pData[0]);
                                counter++;

                            }

                            //create the extrusion data

                            pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, planeAtStart, extrude, 1.0f);
                            pData[0].AxisValues["E5"] = 1.0;
                            pDataList.Add(pData[0]);
                            counter++;
                                
                            pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, planeAtStart, cool, 1.0f);
                            pData[1].AxisValues["E5"] = 1.0;
                            pDataList.Add(pData[1]);

                            counter++;

                            // Loop through each plane in the list
                            for (int l = 0; l < planeInterpolation.Count; l++)
                            {
                                if (planeInterpolation[l] == null || !planeInterpolation[l].IsValid) continue;

                                Plane path = planeInterpolation[l];
                                    
                                pData[2] = new SMTPData(counter, 0, 0, MoveType.Lin, planeInterpolation[l], 0.5f);
                                pData[2].AxisValues["E5"] = 1.4;

                                printedPath.Add(pathCurves[j]);
                                pDataList.Add(pData[2]);
                                allPlanes.Add(path);
                                counter++;
                            }
                        }

                        if (lineDescriptor == "Horizontal")
                        {   

                            //get the first and last plane of the curve
                            Plane pathStart = crvPathPlanes[0];
                            Plane pathEnd = crvPathPlanes[crvPathPlanes.Count - 1];
                            Vector3d pathVector = pathStart.Origin - pathEnd.Origin;
                            pathVector.Reverse();
                            double angle = RhinoMath.ToRadians(-5);
                            pathVector.Rotate(angle, pathEnd.XAxis);

                            double t = 0.5;
                            Curve curve = pathCurves[j];
                            curve.Domain = new Interval(0, 1);
                            Point3d pointOnCurve = curve.PointAt(t);
                            Vector3d tangent = curve.TangentAt(t);

                            //move Z point up 10 to not intersect with node
                            Point3d pathEnd_moveZ = new Point3d(pathEnd.Origin.X, pathEnd.Origin.Y, pathEnd.Origin.Z + 10);
                            Curve curve_newZ = new LineCurve(pathStart.Origin, pathEnd_moveZ);

                            List<Plane> pathZModifiedPlanes = DivideCurveIntoPlanes(curve_newZ, 10);

                            //Define the Y-axis along the direction of the curve
                            Vector3d yAxis = tangent;
                            yAxis.Unitize();

                            //Define the Z-axis perpendicular to the curve direction using a cross product with the world Z-axis
                            Vector3d zAxis = new Vector3d(0, 0, -1);


                            //Calculate the X-axis using the cross product of Y and Z to ensure a right-handed coordinate system
                            Vector3d xAxis = Vector3d.CrossProduct(yAxis, zAxis);
                            xAxis.Unitize();

                            //Construct the plane with the calculated axes
                            Plane plane = new Plane(pointOnCurve, xAxis, yAxis);

                            //define if the extrusion needs a traversal path by calculating the distance between the last curve end and the current curve start
                            //if the distance is greater than 10mm, add a traversal path


                            try
                            {
                                Curve PrevPath = pathCurves[j - 1];
                                Point3d PrevPathStart = PrevPath.PointAtStart;
                                Plane TraversalPathEndPlaneStart = new Plane(PrevPathStart, xAxis, yAxis);
                                Point3d PrevPathEnd = PrevPath.PointAtEnd;
                                Plane TraversalPlanePlaneEnd = new Plane(PrevPathEnd, -Vector3d.XAxis, Vector3d.YAxis);


                                //if the distance between the end of the previous curve and the start of the
                                //current curve is greater than 10mm, add a traversal path
                                if (PrevPathEnd.DistanceTo(pathStart.Origin) > 10)
                                {

                                    pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPlanePlaneEnd, stopExtrude, 3.0f);
                                    pData[0].AxisValues["E5"] = 1.0;
                                    pData[0].Events["NozzleCooling"] = stopCooling;
                                    allPlanes.Add(TraversalPlanePlaneEnd);
                                    pDataList.Add(pData[0]);
                                    counter++;

                                    //create traversal path
                                    //move end point 10mm vertically
                                    Point3d TraversalPath = new Point3d(PrevPathEnd.X, PrevPathEnd.Y, PrevPathEnd.Z + 20);
                                    Plane TraversalPlane = new Plane(TraversalPath, -Vector3d.XAxis, Vector3d.YAxis);
                                    pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPlane, 2.0f);
                                    pData[1].AxisValues["E5"] = 1.0;
                                    allPlanes.Add(TraversalPlane);
                                    pDataList.Add(pData[1]);
                                    counter++;

                                    //move to loction above the current curve at the same Z height of TraversalPath
                                    Point3d TraversalPathEnd = new Point3d(pathStart.Origin.X, pathStart.Origin.Y, TraversalPath.Z);
                                    Plane TraversalPathEndPlane = new Plane(TraversalPathEnd, -Vector3d.XAxis, Vector3d.YAxis);
                                    pData[2] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPathEndPlane, extrude, 3.0f);
                                    pData[2].AxisValues["E5"] = 0.4;
                                    allPlanes.Add(TraversalPathEndPlane);
                                    pDataList.Add(pData[2]);
                                    counter++;
                                }
                                else
                                {
                                    pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, stopExtrude, 3.0f);
                                    pData[0].AxisValues["E5"] = 1.0;

                                    pDataList.Add(pData[0]);
                                    counter++;

                                    pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, stopCooling, 3.0f);
                                    pData[1].AxisValues["E5"] = 1.0;
                                    pDataList.Add(pData[1]);
                                    counter++;
                                }
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, stopExtrude, 3.0f);
                                pData[0].AxisValues["E5"] = 1.0;
                                pData[0].Events["NozzleCooling"] = stopCooling;
                                pDataList.Add(pData[0]);
                                counter++;

                            }
                            int zHeight_test = 301;

                            if (pointOnCurve.Z > zHeight_test)
                            {
                                // Loop through each plane in the list
                                pData[3] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, cool, 3.0f);
        
                                pData[3].Events["Extrude"] = extrude;
                                pDataList.Add(pData[3]);
   
                                counter++;
                                for (int l = 0; l < pathZModifiedPlanes.Count; l++)
                                {
                                    if (pathZModifiedPlanes[l] == null || !pathZModifiedPlanes[l].IsValid) continue;

                                    Plane path = pathZModifiedPlanes[l];

                                    pData[4] = new SMTPData(counter, 0, 0, MoveType.Lin, pathZModifiedPlanes[l], 0.5f);
                                    pData[4].AxisValues["E5"] = 2.0;

                                    pDataList.Add(pData[4]);
                                    allPlanes.Add(path);
                                    counter++;
                                }

                                pData[5] = new SMTPData(counter, 0, 0, MoveType.Lin, pathEnd, stopExtrude, 0.5f);
                                pData[5].AxisValues["E5"] = 2.0;
                                pDataList.Add(pData[5]);
                                printedPath.Add(pathCurves[j]);
                                counter++;
                            }

                            else
                            {
                                // Loop through each plane in the list
                                pData[3] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, stopCooling, 0.5f);
                                pData[3].Events["Extrude"] = extrude;
                                pData[3].AxisValues["E5"] = 1.6;

                                pDataList.Add(pData[3]);
                                for (int l = 0; l < crvPathPlanes.Count; l++)
                                {
                                    if (crvPathPlanes[l] == null || !crvPathPlanes[l].IsValid) continue;

                                    Plane path = crvPathPlanes[l];

                                    pData[4] = new SMTPData(counter, 0, 0, MoveType.Lin, crvPathPlanes[l], 0.5f);
                                    pData[4].AxisValues["E5"] = 1.6;

                                    pDataList.Add(pData[4]);
                                    allPlanes.Add(path);
                                    counter++;
                                }

                                pData[5] = new SMTPData(counter, 0, 0, MoveType.Lin, pathEnd, stopExtrude, 0.5f);
                                pData[5].AxisValues["E5"] = 2.0;
                                pDataList.Add(pData[5]);
                                counter++;

                                printedPath.Add(pathCurves[j]);
                                
                            }

                        }

                        if (lineDescriptor == "Vertical")
                        {


                            //get the first and last plane of the curve
                            Plane pathStart = crvPathPlanes[0];
                            Plane pathEnd = crvPathPlanes[crvPathPlanes.Count - 1];
                            double t = 0.5;
                            Curve curve = pathCurves[j];
                            curve.Domain = new Interval(0, 1);
                            Point3d pointOnCurve = curve.PointAt(t);
                            Vector3d tangent = curve.TangentAt(t);
                            Vector3d zAxis = pathStart.Origin - pathEnd.Origin;

                            //Get the parameter to start the circle motion 2mm above pathstart
                            double startExtruding = 2.5;
                            double startCooling = 2.0;


                            //get start extruding point 2mm above the start of the curve in the Z direction
                            Point3d startExtruding_pt = new Point3d(pathStart.Origin.X, pathStart.Origin.Y, pathStart.Origin.Z + startExtruding);

                            LineCurve modifiedPath = new LineCurve(startExtruding_pt, curve.PointAtEnd);

                            //get the parameter of the curve at the startExtruding length
                            modifiedPath.LengthParameter(startCooling, out double startCoolingParam);
                            Point3d startCooling_pt = modifiedPath.PointAt(startCoolingParam);

                            zAxis.Unitize();

                            //Calculate the initial X-axis to be perpendicular to Z and aligned to the "left" of the curve direction
                            Vector3d xAxis = Vector3d.CrossProduct(Vector3d.ZAxis, zAxis);
                            xAxis.Unitize();

                            xAxis = Vector3d.CrossProduct(Vector3d.YAxis, zAxis);
                            xAxis.Unitize();

                            //Calculate the Y-axis to complete the orthogonal system
                            Vector3d yAxis = Vector3d.CrossProduct(zAxis, xAxis);
                            yAxis.Unitize();


                            //Construct the plane with the calculated axes
                            Plane plane = new Plane(pointOnCurve, xAxis, yAxis);
                            Plane planeAtEnd = new Plane(pathEnd.Origin, xAxis, yAxis);
                            Plane planeAtStart = new Plane(pathStart.Origin, xAxis, yAxis);
                            Plane startExtrudingPlane = new Plane(startExtruding_pt, xAxis, yAxis);
                            Plane stopExtrudingPlane = new Plane(curve.PointAtEnd, xAxis, yAxis);
                            Plane startCooling_plane = new Plane(startCooling_pt, xAxis, yAxis);



                            //Get the plane orientation of the curve based on the start and end point
                            List<Plane> planeInterpolation = Quaternion_interpolation.interpolation(pathCurves[j], planeAtStart, planeAtEnd, numCrvPathPlanes);


                            //create the extrusion data

                            //define if the extrusion needs a traversal path by calculating the distance between the last curve end and the current curve start
                            //if the distance is greater than 10mm, add a traversal path
                            try
                            {
                                Curve PrevPath = pathCurves[j - 1];
                                Point3d PrevPathStart = PrevPath.PointAtStart;
                                Plane TraversalPathEndPlaneStart = new Plane(PrevPathStart, xAxis, yAxis);
                                Point3d PrevPathEnd = PrevPath.PointAtEnd;
                                Plane TraversalPlanePlaneEnd = new Plane(PrevPathEnd, -Vector3d.XAxis, Vector3d.YAxis);


                                //if the distance between the end of the previous curve and the start of the
                                //current curve is greater than 10mm, add a traversal path
                                if (PrevPathEnd.DistanceTo(pathStart.Origin) > 10)
                                {

                                    pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPlanePlaneEnd, stopExtrude, 3.0f);
                                    pData[0].AxisValues["E5"] = 1.0;
                                    pData[0].Events["NozzleCooling"] = stopCooling;
                                    allPlanes.Add(TraversalPlanePlaneEnd);
                                    pDataList.Add(pData[0]);
                                    counter++;

                                    //create traversal path
                                    //move end point 10mm vertically
                                    Point3d TraversalPath = new Point3d(PrevPathEnd.X, PrevPathEnd.Y, PrevPathEnd.Z + 30);
                                    Plane TraversalPlane = new Plane(TraversalPath, -Vector3d.XAxis, Vector3d.YAxis);
                                    pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPlane, 2.0f);
                                    pData[1].AxisValues["E5"] = 1.0;
                                    allPlanes.Add(TraversalPlane);
                                    pDataList.Add(pData[1]);
                                    counter++;

                                    //move to loction above the current curve at the same Z height of TraversalPath
                                    Point3d TraversalPathEnd = new Point3d(pathStart.Origin.X, pathStart.Origin.Y, TraversalPath.Z);
                                    Plane TraversalPathEndPlane = new Plane(TraversalPathEnd, -Vector3d.XAxis, Vector3d.YAxis);
                                    pData[2] = new SMTPData(counter, 0, 0, MoveType.Lin, TraversalPathEndPlane, stopExtrude, 3.0f);
                                    pData[2].AxisValues["E5"] = 1.0;
                                    allPlanes.Add(TraversalPathEndPlane);
                                    pDataList.Add(pData[2]);
                                    counter++;
                                }
                                else
                                {
                                    pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, startExtrudingPlane, stopExtrude, 3.0f);
                                    pData[0].AxisValues["E5"] = 1.0;

                                    pDataList.Add(pData[0]);
                                    counter++;

                                    pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, startExtrudingPlane, stopCooling, 3.0f);
                                    pData[1].AxisValues["E5"] = 1.0;
                                    pDataList.Add(pData[1]);
                                    counter++;
                                }
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, startExtrudingPlane, stopExtrude, 3.0f);
                                pData[0].AxisValues["E5"] = 1.0;
                                pData[0].Events["NozzleCooling"] = stopCooling;
                                pDataList.Add(pData[0]);
                                counter++;

                            }


                            //define the circle motion for the start of the extrusion
                            Circle circle = new Circle(startExtrudingPlane, 1);

                            //start origin of extrusion path
                            pData[3] = new SMTPData(counter, 0, 0, MoveType.Lin, startExtrudingPlane, 3.0f);
                            pData[3].AxisValues["E5"] = 2.4;
                            pDataList.Add(pData[3]);
                            counter++;

                            //doc.Objects.AddCircle(circle);
                            //doc.Views.Redraw();
                            int numPlanes = 10;
                            List<Plane> circlePathPlanes = DivideCurveIntoPlanes(circle.ToNurbsCurve(), numPlanes);
                            //loop through the circle planes
                            for (int k = 0; k < circlePathPlanes.Count; k++)
                            {
                                Plane cirPath = circlePathPlanes[k];

                                pData[4] = new SMTPData(counter, 0, 0, MoveType.Lin, cirPath, 0.1f);
                                pData[4].AxisValues["E5"] = 2.4;
                                pData[4].Events["Extrude"] = extrude;


                                pDataList.Add(pData[4]);
                                allPlanes.Add(cirPath);
                                counter++;
                            }

                            pData[5] = new SMTPData(counter, 0, 0, MoveType.Lin, startCooling_plane, 3.0f);
                            pData[5].AxisValues["E5"] = 2.4;
                            pData[5].Events["NozzleCooling"] = cool;

                            pDataList.Add(pData[5]);
                            counter++;
                            allPlanes.Add(startCooling_plane);

                            pData[6] = new SMTPData(counter, 0, 0, MoveType.Lin, stopExtrudingPlane, 0.22f);
                            pData[6].AxisValues["E5"] = 2.4;
                            pData[6].Events["Extrude"] = stopExtrude;

                            pDataList.Add(pData[6]);
                            counter++;
                            allPlanes.Add(stopExtrudingPlane);

                            pData[7] = new SMTPData(counter, 0, 0, MoveType.Lin, stopExtrudingPlane, 0.22f);
                            pData[7].AxisValues["E5"] = 2.4;
                            pData[7].Events["CycleWait"] = cycleWait;

                            pDataList.Add(pData[7]);
                            counter++;

                            pData[8] = new SMTPData(counter, 0, 0, MoveType.Lin, stopExtrudingPlane, 0.22f);
                            pData[8].AxisValues["E5"] = 2.4;
                            pData[8].Events["NozzleCooling"] = stopCooling;

                            pDataList.Add(pData[8]);
                            counter++;
                        }
                    }

                    //store all the pointdata and then instantiate the shape outside of the loop
                    Guid guid = Guid.NewGuid();

                    smtPlugin.UserData[guid] = pDataList.ToArray();
                    shapes[0] = SuperShape.SuperShapeFactory(guid, null, DivisionStyle.PointData, ZOrientStyle.PointData, VectorStyle.ByParam, YOrientStyle.PointData, false, 0.0, Rhino.Geometry.Plane.WorldXY);

                    if (shapes.Length > 0)
                    {
                        var spbs = opUI.ReadFromGH(shapes);
                        if (spbs != null)
                        {
                            spbs.Last().IsSelected = true;
                            opUI.IsSelected = true;
                            //spbs.Last().IsSelected = true;
                        }
                    }

                }
                else
                    RhinoApp.WriteLine("You must select an Operation");
            }
            else
                RhinoApp.WriteLine("You must select an Operation");

            // 3. Set the outputs
            DA.SetDataList(0, allPlanes);
            //DA.SetDataList(2, pData_points);

        }



        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("8ffc584c-b45e-4f5f-bd12-760adc0f4b2d");
    }
}