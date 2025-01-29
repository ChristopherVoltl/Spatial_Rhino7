using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System.Linq;
using Rhino.Render.ChangeQueue;
using System.Runtime.Remoting.Messaging;
using System.Collections;
using Rhino.Geometry.Intersect;
using Rhino.UI;
using System.Diagnostics.Eventing.Reader;
using Compas.Robot.Link;
using System.Security.Cryptography;

namespace Spatial_Rhino7.Spatial_Printing_Components
{
    public class ContinuousToolpathMethods : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CurvePlaneGenerator class.
        /// </summary>
        public ContinuousToolpathMethods()
          : base("Continuous Toolpath Methods", "CTM",
              "Generates a graph from a set of lines and nodes and applies differnt graphing Algorithms to the neetwork to find continuous paths",
              "FGAM", "Toolpathing")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("pathCurves", "pC", " an array of Curves", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("Longest Trail", "LT", "Graph:   Longest Trail Algorithm", GH_ParamAccess.tree);
            pManager.AddPointParameter("Graph_Nodes", "GN", "Graph: Nodes", GH_ParamAccess.tree);

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        public class latticeStructuralAnalysis
        {
        }


        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> curves = new List<Curve>();
            // Retrieve the input data
            if (!DA.GetDataList(0, curves)) return;

            // Create the graph
            var graph = new GraphLongestTrail(curves);

            // Set a 5-second timeout
            TimeSpan timeout = TimeSpan.FromSeconds(20);

            List<Line> longestTrail = graph.FindLongestTrail(timeout);

            // Set the output data
            DA.SetDataList(0, longestTrail);



        }



        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("c4f90abe-0662-4a46-9b9f-c7b521530ed1"); }
        }
    }
}