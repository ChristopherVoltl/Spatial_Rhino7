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
        /// This is the method that actually does the work.
        /// </summary>
        /// 

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Declare placeholder variables and assign initial invalid data.
            //    This way, if the input parameters fail to supply valid data, we know when to abort.
            var pathPlanes = new List<Plane>();
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
            List<Plane> planes = new List<Plane>();
            List<Point3d> pData_points = new List<Point3d>();

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

                    // Divide the segment into 10 equal parts
                    int divisionCount = 10;
                    double[] divisionParams = segment.DivideByCount(divisionCount, true);
                    if (divisionParams == null) continue;

                    // Create planes at division points
                    for (int i = 0; i < divisionParams.Length; i++)
                    {
                        Point3d pointOnSegment = segment.PointAt(divisionParams[i]);
                        Vector3d zVector = new Vector3d(0, 0, -1);
                        Plane plane = new Plane(pointOnSegment, zVector);
                        planes.Add(plane);
                    }

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
                    //extrude actionstates
                    SuperEvent extrude = new SuperEvent(extrudeAct, 0.0, EventType.Activate, true);
                    SuperEvent stopExtrude = new SuperEvent(extrudeAct, 0.0, EventType.Deactivate, true);
                    //fan actionstates
                    //SuperEvent fan = new SuperEvent(fanAct, 0.0, EventType.Activate, true);
                    //SuperEvent stopFan = new SuperEvent(fanAct, 0.0, EventType.Deactivate, true);
                    //nozzle cooling actionstates
                    //SuperEvent nozzleCooling = new SuperEvent(nozzleAct, 0.0, EventType.Activate, true);
                    //SuperEvent stopnozzleCooling = new SuperEvent(nozzleAct, 0.0, EventType.Deactivate, true);


                    //given an array of ordered and oriented planes for each spatial extrusion location
                    //build paths

                    //input curve and slicing parameters

                    SuperShape[] shapes = new SuperShape[planes.Count];

                    //Assinging SMT functions based in the length of the curve
                    List<Curve> processedCurves = new List<Curve>();
                    

                    // Loop through each curve in the list
                    foreach (Curve curve in splitCurves)
                    {
                        if (curve == null || !curve.IsValid) continue;

                        // Get the length of the current curve
                        double curveLength = curve.GetLength();

                        // Example: If the curve length is greater than the threshold, perform one operation
                        if (curveLength > 10)
                        {
                            RhinoApp.WriteLine("greater");
                            //we can use action states or events. try events first
                            for (int i = 0; i < planes.Count; i++)

                            {

                                SMTPData[] pData = new SMTPData[1];

                                //for each point, create a safe approach, start extrusion, extrude path, end extrusion. Then cycle back through the paths
                                Plane path = planes[i];

                                //create the extrusion data
                                //pData[0] = new SMTPData(0, 0, 0, MoveType.Joint, safe0, 1.0f);
                                //pData[1] = new SMTPData(1, 1, 1, MoveType.Lin, approachSegment, 1.0f);
                                pData[0] = new SMTPData(0, 0, 0, MoveType.Lin, path, extrude, 1.0f);
                                //pData[1] = new SMTPData(5, 5, 5, MoveType.Joint, place, stopExtrude, 1.0f);
                                //pData[4] = new SMTPData(6, 6, 6, MoveType.Joint, safe1, 1.0f);

                                //finished with path
                                Guid guid = Guid.NewGuid();
                                smtPlugin.UserData[guid] = pData;
                                pData_points.Add(pData[0].Origin);

                                shapes[i] = SuperShape.SuperShapeFactory(guid, null, DivisionStyle.PointData, ZOrientStyle.PointData, VectorStyle.ByParam, YOrientStyle.PointData, false, 0.0, Rhino.Geometry.Plane.WorldXY);
                                //smtPlugin.UserGeometry[guid] = partObjs[i].ExtrusionGeometry;


                            }
                        }
                        else
                        {
                            RhinoApp.WriteLine("less");
                            //we can use action states or events. try events first
                            for (int i = 0; i < planes.Count; i++)

                            {

                                SMTPData[] pData = new SMTPData[1];

                                //for each point, create a safe approach, start extrusion, extrude path, end extrusion. Then cycle back through the paths
                                Plane path = planes[i];

                                //create the extrusion data
                                //pData[0] = new SMTPData(0, 0, 0, MoveType.Joint, safe0, 1.0f);
                                //pData[1] = new SMTPData(1, 1, 1, MoveType.Lin, approachSegment, 1.0f);
                                pData[0] = new SMTPData(0, 0, 0, MoveType.Lin, path, extrude, 0.1f);
                                //pData[1] = new SMTPData(5, 5, 5, MoveType.Joint, place, stopExtrude, 1.0f);
                                //pData[4] = new SMTPData(6, 6, 6, MoveType.Joint, safe1, 1.0f);

                                //finished with path
                                Guid guid = Guid.NewGuid();
                                smtPlugin.UserData[guid] = pData;

                                shapes[i] = SuperShape.SuperShapeFactory(guid, null, DivisionStyle.PointData, ZOrientStyle.PointData, VectorStyle.ByParam, YOrientStyle.PointData, false, 0.0, Rhino.Geometry.Plane.WorldXY);
                                //smtPlugin.UserGeometry[guid] = partObjs[i].ExtrusionGeometry;


                            }
                        }
                    }

                   
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
            DA.SetDataList(0, splitCurves);
            DA.SetDataList(1, planes);
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