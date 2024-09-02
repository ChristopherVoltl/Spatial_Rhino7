using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using SMT;
using static SMT.SMTUtilities;
using Rhino;
using System.Linq;

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
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
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
 

            // 2. Retrieve input data.
            if (!DA.GetDataList(0, pathPlanes)) { return; }



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
                    SuperEvent extrude = new SuperEvent(extrudeAct, 0.0, EventType.Activate, true);
                    SuperEvent stopExtrude = new SuperEvent(extrudeAct, 0.0, EventType.Deactivate, true);

                    //given an array of ordered and oriented planes for each spatial extrusion location
                    //build paths
                    Point3d segmentStartPt = new Point3d(1375, -1892, 47);  //needs to be revised to be varible start of the path 
                    Vector3d segmentStartZ = new Vector3d(0, 1, 0);
                    Vector3d segmentStartX = new Vector3d(1, 0, 0);
                    Vector3d segmentStartY = new Vector3d(0, 0, -1);

                    Plane segmentStart = new Plane(segmentStartPt, segmentStartX, segmentStartY);
                    Plane approachSegment = new Plane(segmentStart);//move along -Z of tool
                    Plane safe0 = approachSegment;//move up from approach  on World Z

                    //approach the start of the extrusion path

                    SuperShape[] shapes = new SuperShape[pathPlanes.Count];
                    approachSegment.Translate(segmentStartZ * -50); //move negative into the path to account for thickess of the extrusion for bonding


                    //endPoint for all paths
                    Point3d endPt = new Point3d(1236, 0, 1570); //needs to be revised to be varible end of the path 
                    Vector3d endZ = new Vector3d(0, 0, -1);
                    Vector3d endX = new Vector3d(-1, 0, 0);
                    Vector3d endY = new Vector3d(0, 1, 0);
                    Plane endPl = new Plane(endPt, endX, endY);
                    //we can use action states or events. try events first
                    for (int i = 0; i < pathPlanes.Count; i++)

                    {
                        SMTPData[] pData = new SMTPData[5];

                        //for each point, create a safe approach, start extrusion, extrude path, end extrusion. Then cycle back through the paths
                        Plane place = pathPlanes[i];
                        Plane approachPlace = place;//World Z from Place
                        approachPlace.Translate(Vector3d.ZAxis * 100);
                        Plane safe1 = approachPlace;
                        safe1.Translate(Vector3d.ZAxis * 50);
                        //create the extrusion data
                        pData[0] = new SMTPData(0, 0, 0, MoveType.Joint, safe0, 1.0f);
                        pData[1] = new SMTPData(1, 1, 1, MoveType.Lin, approachSegment, 1.0f);
                        pData[2] = new SMTPData(2, 2, 2, MoveType.Lin, segmentStart, extrude, 1.0f);
                        pData[3] = new SMTPData(5, 5, 5, MoveType.Joint, endPl, stopExtrude, 1.0f);
                        pData[4] = new SMTPData(6, 6, 6, MoveType.Joint, safe1, 1.0f);

                        //finished with path
                        Guid guid = Guid.NewGuid();
                        smtPlugin.UserData[guid] = pData;

                        shapes[i] = SuperShape.SuperShapeFactory(guid, null, DivisionStyle.PointData, ZOrientStyle.PointData, VectorStyle.ByParam, YOrientStyle.PointData, false, 0.0, Rhino.Geometry.Plane.WorldXY);
                        //smtPlugin.UserGeometry[guid] = partObjs[i].ExtrusionGeometry;

                        //DA.SetData(0, shapes[i]);

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