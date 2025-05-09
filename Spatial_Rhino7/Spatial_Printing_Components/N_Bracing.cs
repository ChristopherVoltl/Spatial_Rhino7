using System;
using System.Collections.Generic;
using Rhino;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Collections;

namespace Spatial_Rhino7.Spatial_Printing_Components
{
    public class N_Bracing : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CurvePlaneGenerator class.
        /// </summary>
        public N_Bracing()
          : base("Find Vertical - Angled Connections", "N",
              "Reorders curve list to find curves connected to the end point of vertical curves",
              "FGAM", "Sorting")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("pathCurves", "pC", "a list of Curves", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("pathCurves", "pC", "Resorted curves", GH_ParamAccess.list);
        }

        public class CurveReorderer
        {
            private readonly List<Curve> _curves;
            private readonly double _tolerance;
            private readonly double _verticalThresholdDeg;

            public CurveReorderer(List<Curve> curves, double tolerance = 1e-6, double verticalThresholdDeg = 5.0)
            {
                _curves = curves;
                _tolerance = tolerance;
                _verticalThresholdDeg = verticalThresholdDeg;
            }

            public List<Curve> Reorder()
            {
                for (int i = 0; i < _curves.Count; i++)
                {
                    Curve current = _curves[i];

                    // Handle vertical curve logic
                    if (IsVertical(current))
                    {
                        Point3d endPt = current.PointAtEnd;

                        for (int j = i + 1; j < _curves.Count; j++)
                        {
                            Curve candidate = _curves[j];
                            Point3d startPt = candidate.PointAtStart;
                            Point3d endCandidatePt = candidate.PointAtEnd;

                            bool connected = endPt.DistanceTo(startPt) < _tolerance || endPt.DistanceTo(endCandidatePt) < _tolerance;

                            if (connected && IsAngled(candidate))
                            {
                                _curves.RemoveAt(j);
                                _curves.Insert(i + 1, candidate);
                                break;
                            }
                        }
                    }

                    // Handle angled curve logic
                    if (IsAngled(current))
                    {
                        // Find the higher endpoint of the angled curve
                        Point3d topPt = current.PointAtStart.Z > current.PointAtEnd.Z ? current.PointAtStart : current.PointAtEnd;

                        for (int j = i + 1; j < _curves.Count; j++)
                        {
                            Curve candidate = _curves[j];
                            if (!IsVertical(candidate)) continue;

                            Point3d startPt = candidate.PointAtStart;
                            Point3d endPt = candidate.PointAtEnd;

                            bool connectsAtTop = startPt.DistanceTo(topPt) < _tolerance || endPt.DistanceTo(topPt) < _tolerance;

                            if (connectsAtTop)
                            {
                                current.Reverse(); // Reverse the vertical curve to match the angled curve's direction
                                _curves.RemoveAt(j);
                                _curves.Insert(i, candidate);
                                i--; // to reevaluate current position after inserting vertical before it
                                break;
                            }
                        }
                    }
                }

                return _curves;
            }

            private bool IsVertical(Curve curve)
            {
                if (!curve.IsLinear())
                    return false;

                Vector3d dir = curve.TangentAtStart;
                return Math.Abs(dir.X) < _tolerance && Math.Abs(dir.Y) < _tolerance;
            }

            private bool IsAngled(Curve curve)
            {
                if (!curve.IsLinear())
                    return false;

                Vector3d dir = curve.TangentAtStart;
                double angle = Vector3d.VectorAngle(dir, Vector3d.ZAxis) * (180.0 / Math.PI);
                return angle > _verticalThresholdDeg;
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> pathCurves = new List<Curve>();

            if (!DA.GetDataList(0, pathCurves)) { return; }

            var reorderer = new CurveReorderer(pathCurves);
            List<Curve> reordered = reorderer.Reorder();

            // 3. Set the outputs
            DA.SetDataList(0, reordered);
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
            get { return new Guid("218b4d60-1c1d-4d8f-a5a6-e4398e0c59eb"); }
        }
    }
}