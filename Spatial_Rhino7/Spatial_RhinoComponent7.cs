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

        /// <summary>
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



            public class LineOrientation
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

            public static String OrientLine(Curve curve)
                {

                    ///Horizontal Line
                    if (curve.PointAtStart.Z == curve.PointAtEnd.Z)
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
                    actionPauseUI.StartValue = "0.0";
                    actionPauseUI.EndValue = "1.0";
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


                    //given an array of ordered and oriented planes for each spatial extrusion location
                    //build paths

                    //input curve and slicing parameters

                    SuperShape[] shapes = new SuperShape[1];

                    //Assinging SMT functions based in the length of the curve
                    List<SMTPData> pDataList = new List<SMTPData>();

                    SMTPData[] pData = new SMTPData[7];
                    int counter = 0;
                    //loop through each path line or polyline 
                    for (int j = 0; j < pathCurves.Count; j++)
                    {
                        if (pathCurves[j] == null || !pathCurves[j].IsValid) continue;

                        //get the planes for each path
                        //List<Plane> crvPathPlanes = PolylinePlaneDivider.DividePolylineByLength(pathCurves[j], 10);                     
                        
                        List<Curve> segments = LineOrientation.ExplodeCurves(pathCurves[j]);

                        //if the polyline is comprised of more than one curve loop through each curve
                        for (int i = 0; i < segments.Count; i++)
                        {
                            if (segments[i] == null || !segments[i].IsValid) continue;

                            //get the planes for each path
                            int numCrvPathPlanes = 10;
                            List<Plane> crvPathPlanes = DivideCurveIntoPlanes(segments[i], numCrvPathPlanes);

                            //for each point, start extrusion, extrude path, end extrusion.

                            //for each curve define if the curve is angled up, down, horizontal or vertical
                            string lineDescriptor = LineOrientation.OrientLine(segments[i]);

                            //if the curve is angled up
                            if (lineDescriptor == "AngledUp")
                            {
                                //get the first and last plane of the curve
                                Plane pathStart = crvPathPlanes[0];
                                Plane pathEnd = crvPathPlanes[crvPathPlanes.Count - 1];
                                //create the extrusion data
                                pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, extrude, 1.0f);
                                pDataList.Add(pData[0]);
                                counter++;
                                pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, cool, 1.0f);
                                pDataList.Add(pData[1]);
                                counter++;
                                
                                //define the circle motion for the start of the extrusion
                                Circle circle = new Circle(pathStart, 3);
                                
                                //doc.Objects.AddCircle(circle);
                                //doc.Views.Redraw();
                                int numPlanes = 10;
                                List<Plane> circlePathPlanes = DivideCurveIntoPlanes(circle.ToNurbsCurve(), numPlanes);
                                //loop through the circle planes
                                for (int k = 0; k < circlePathPlanes.Count; k++)
                                {
                                    Plane cirPath = circlePathPlanes[k];
                                    pData[2] = new SMTPData(counter, 0, 0, MoveType.Lin, cirPath, 0.5f);
                                    pDataList.Add(pData[2]);
                                    allPlanes.Add(cirPath);
                                    doc.Objects.AddPoint(cirPath.Origin);
                                    doc.Views.Redraw();
                                    counter++;
                                }

                                pData[3] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, 1.0f);
                                counter++;

                                // Loop through each plane in the list
                                for (int l = 0; l < crvPathPlanes.Count; l++)
                                {
                                    if (crvPathPlanes[l] == null || !crvPathPlanes[l].IsValid) continue;

                                    Plane path = crvPathPlanes[l];
                                    //if the plane is the last plane of the curve
                                    if (l > (80/100)*numCrvPathPlanes)
                                        {
                                        pData[4] = new SMTPData(counter, 0, 0, MoveType.Lin, crvPathPlanes[l], 0.2f);
                                    }
                                    else
                                    {
                                        pData[4] = new SMTPData(counter, 0, 0, MoveType.Lin, crvPathPlanes[l], 0.5f);
                                    }

                                    pDataList.Add(pData[4]);
                                    allPlanes.Add(path);
                                    counter++;
                                }

                                pData[5] = new SMTPData(counter, 0, 0, MoveType.Lin, pathEnd, stopCooling, 1.0f);
                                pDataList.Add(pData[5]);
                                counter++;
                                pData[6] = new SMTPData(counter, 0, 0, MoveType.Lin, pathEnd, stopExtrude, 1.0f);
                                pDataList.Add(pData[6]);
                                counter++;
                                //add new point data to the list of points 
                                //pData[1] = new SMTPData(counter + 1, 0, 0, MoveType.Lin, pathStart, stopcycleWait, 1.0f);
                                //
                                //counter++;
                            }

                            if (lineDescriptor == "AngledDown")
                            {
                                //get the first and last plane of the curve
                                Plane pathStart = crvPathPlanes[0];
                                Plane pathEnd = crvPathPlanes[crvPathPlanes.Count - 1];
                                //create the extrusion data
                                pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, extrude, 1.0f);
                                pDataList.Add(pData[0]);
                                counter++;
                                pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, cool, 1.0f);
                                pDataList.Add(pData[1]);
                                counter++;

                                // Loop through each plane in the list
                                for (int l = 0; l < crvPathPlanes.Count; l++)
                                {
                                    if (crvPathPlanes[l] == null || !crvPathPlanes[l].IsValid) continue;

                                    Plane path = crvPathPlanes[l];
                                    
                                    pData[3] = new SMTPData(counter, 0, 0, MoveType.Lin, crvPathPlanes[l], 2.0f);
                                
                                    

                                    pDataList.Add(pData[4]);
                                    allPlanes.Add(path);
                                    counter++;
                                }

                                pData[5] = new SMTPData(counter, 0, 0, MoveType.Lin, pathEnd, stopCooling, 1.0f);
                                pDataList.Add(pData[4]);
                                counter++;
                                pData[6] = new SMTPData(counter, 0, 0, MoveType.Lin, pathEnd, stopExtrude, 1.0f);
                                pDataList.Add(pData[5]);
                                counter++;
                            }
                        //for each point, start extrusion, extrude path, end extrusion.

                        //Plane pathStart = crvPathPlanes[0];
                        //Plane pathEnd = crvPathPlanes[crvPathPlanes.Count - 1];
                        //create the extrusion data
                        //pData[0] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, extrude, 1.0f);
                        //counter++;
                        //pData[1] = new SMTPData(counter, 0, 0, MoveType.Lin, pathStart, cool, 1.0f);
                        //counter++;
                        //pData[1] = new SMTPData(counter + 1, 0, 0, MoveType.Lin, pathStart, cycleWait, 1.0f);
                        //add new point data to the list of points 
                        //pData[1] = new SMTPData(counter + 1, 0, 0, MoveType.Lin, pathStart, stopcycleWait, 1.0f);
                        //
                        //counter++;


                        
   
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