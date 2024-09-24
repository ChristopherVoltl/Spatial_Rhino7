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
            pManager.AddPlaneParameter("pathPlanes ", "pP", " an array of Planes", GH_ParamAccess.list);
            pManager.AddCurveParameter("pathCurves", "pC", " an array of Curves", GH_ParamAccess.list);
            pManager.AddNumberParameter("Parameter", "P", "Parameter to split the curves at (between 0 and 1)", GH_ParamAccess.item, 0.9);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Split Curves", "SC", "Resulting split curves", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Planes", "PL", "Planes created at division points", GH_ParamAccess.list);
            pManager.AddPointParameter("SuperShape", "SS", "SuperShape for each plane", GH_ParamAccess.list);
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
        }


        /// </summary>
        /// 

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Declare placeholder variables and assign initial invalid data.
            //    This way, if the input parameters fail to supply valid data, we know when to abort.
            List<Plane> pathPlanes = new List<Plane>();
            var pathCurves = new List<Curve>();
            double parameter = 0.5; // Default parameter


            // 2. Retrieve input data.
            if (!DA.GetDataList(0, pathPlanes)) { return; }
            if (!DA.GetDataList(1, pathCurves)) { return; }
            if (!DA.GetData(2, ref parameter)) return;

            // 3. Abort on invalid inputs.
            if (pathCurves == null)
            {
                RhinoApp.WriteLine("The selected object is not a curve.");
            }

            List<Curve> splitCurves = new List<Curve>();
            List<Curve> orderedCurves = new List<Curve>();
            List<Point3d> pData_points = new List<Point3d>();
            List<Plane> allPlanes = new List<Plane>();

            foreach (Curve curve in pathCurves)
            {
                if (curve == null || !curve.IsValid) continue;

                // Get the curve's length and split it at the specified parameter
                
                curve.Domain = new Interval(0, 1); // Normalize the parameter if it's between 0 and 1
                    
                Curve[] splitResult = curve.Split(parameter); // Split the curve at the normalized parameter
                if (splitResult == null || splitResult.Length != 2) continue;

                // Process each resulting curve after the split
                foreach (Curve segment in splitResult)
                {
                    if (segment == null || !segment.IsValid) continue;

                    // Add the segment curve to the list
                    splitCurves.Add(segment);
                }
            }



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
                    //extrude actionstates
                    SuperEvent extrude = new SuperEvent(extrudeAct, 0.0, EventType.Activate, true);
                    SuperEvent stopExtrude = new SuperEvent(extrudeAct, 0.0, EventType.Deactivate, true);
                    //fan actionstates
                    SuperEvent cool = new SuperEvent(nozzleCoolingAct, 0.0, EventType.Activate, true);
                    SuperEvent stopCooling = new SuperEvent(nozzleCoolingAct, 0.0, EventType.Deactivate, true);
                    //nozzle cooling actionstates
                    //SuperEvent nozzleCooling = new SuperEvent(nozzleAct, 0.0, EventType.Activate, true);
                    //SuperEvent stopnozzleCooling = new SuperEvent(nozzleAct, 0.0, EventType.Deactivate, true);


                    //given an array of ordered and oriented planes for each spatial extrusion location
                    //build paths

                    //input curve and slicing parameters

                    SuperShape[] shapes = new SuperShape[1];

                    //Assinging SMT functions based in the length of the curve
                    List<Curve> processedCurves = new List<Curve>();
                    List<SMTPData> pDataList = new List<SMTPData>();

                    SMTPData[] pData = new SMTPData[5];

                    //loop through each path line or polyline 
                    //for (int i = 0; i < pathCurves.Count; i++)
                    //{
                    //    if (pathCurves[i] == null || !pathCurves[i].IsValid) continue;


                    //}
                    //for each path, divide into planes


                    //for each point, start extrusion, extrude path, end extrusion.

                    Plane pathStart = pathPlanes[0];
                    Plane pathEnd = pathPlanes[pathPlanes.Count - 1];
                    //create the extrusion data
                    pData[0] = new SMTPData(0, 0, 0, MoveType.Lin, pathStart, extrude, 1.0f);
                    pData[1] = new SMTPData(1, 0, 0, MoveType.Lin, pathStart, cool, 1.0f);

                    pDataList.Add(pData[0]);
                    pDataList.Add(pData[1]);
                    // Loop through each curve in the list
                    for (int i = 0; i < pathPlanes.Count; i++)
                    {
                        if (pathPlanes[i] == null || !pathPlanes[i].IsValid) continue;


                        Plane path = pathPlanes[i];

                        pData[2] = new SMTPData(i+2, 0, 0, MoveType.Lin, path, 1.0f);



                        //pData[1] = new SMTPData(5, 5, 5, MoveType.Joint, place, stopExtrude, 1.0f);
                        //pData[4] = new SMTPData(6, 6, 6, MoveType.Joint, safe1, 1.0f);

                        //finished with path
                        //Guid guid = Guid.NewGuid();
                        //smtPlugin.UserData[guid] = pData;
                        //pData_points.Add(pData[0].Origin);
                        //allPlanes.Add(pathPlanes[i]);
                        //pDataList.Add(pData[0]);
                        pDataList.Add(pData[2]);
                        


                        //pData_points.Add(pData[0].Origin);
                        //shapes[0] = SuperShape.SuperShapeFactory(guid, null, DivisionStyle.PointData, ZOrientStyle.PointData, VectorStyle.ByParam, YOrientStyle.PointData, false, 0.0, Rhino.Geometry.Plane.WorldXY);
                        //smtPlugin.UserGeometry[guid] = partObjs[i].ExtrusionGeometry;

                    }

                    pData[3] = new SMTPData(pathPlanes.Count + 1, 0, 0, MoveType.Lin, pathEnd, stopCooling, 1.0f);
                    pData[4] = new SMTPData(pathPlanes.Count + 2, 0, 0, MoveType.Lin, pathEnd, stopExtrude, 1.0f);


                    pDataList.Add(pData[3]);
                    pDataList.Add(pData[4]);

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
            DA.SetDataList(0, orderedCurves);
            DA.SetDataList(1, allPlanes);
            DA.SetDataList(2, pData_points);

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