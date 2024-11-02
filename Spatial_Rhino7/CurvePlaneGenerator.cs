﻿using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Spatial_Rhino7
{
    public class CurvePlaneGenerator : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CurvePlaneGenerator class.
        /// </summary>
        public CurvePlaneGenerator()
          : base("CurvePlaneGenerator", "CrvPlnGen",
              "Generates planes on the curves to be used to determine the robotic extrusion orientation",
              "FGAM", "Planes")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("pathCurves", "pC", " an array of Curves", GH_ParamAccess.list);
            pManager.AddNumberParameter("Max Rotation Angle", "Max Ro", "Max Plane Rotaion Angle", GH_ParamAccess.item, 45.0);
            pManager.AddNumberParameter("Curve Parameters t2", "t1", "Max Plane Rotaion Angle", GH_ParamAccess.item, 0.25);
            pManager.AddNumberParameter("Curve Parameter t2", "t2", "Max Plane Rotaion Angle", GH_ParamAccess.item, 0.25);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Planes", "PL", "Planes created in respects to the robotic tilt angle", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        /// 
        public class PlaneGenerator
        {
            // Method to generate a plane at a specific parameter along the curve
            public Plane GeneratePlaneAt(Curve curve, double t, bool alignToCurve = true)
            {
                if (!curve.IsValid || !curve.Domain.IncludesParameter(t))
                    throw new ArgumentException("Invalid parameter on the curve");

                // Find the point and tangent at parameter t
                Point3d point = curve.PointAt(t);
                Vector3d tangent = curve.TangentAt(t);

                // World-aligned or curve-aligned plane
                Plane plane = alignToCurve
                    ? new Plane(point, tangent)
                    : new Plane(point, Vector3d.XAxis, Vector3d.YAxis);

                // Set the Z-axis perpendicular to the curve direction if curve-aligned
                if (alignToCurve)
                {
                    plane.ZAxis = tangent;
                    plane.XAxis = Vector3d.CrossProduct(plane.YAxis, plane.ZAxis);
                }

                return plane;
            }

            // Method to rotate plane around its X-axis up to a max angle
            public Plane RotatePlaneAroundX(Plane plane, double rotationAngle, double maxRotationAngle)
            {
                // Constrain rotation angle to maxRotationAngle
                double clampedAngle = Math.Min(rotationAngle, maxRotationAngle);
                double angleInRadians = RhinoMath.ToRadians(clampedAngle);

                // Perform rotation around the plane's X-axis
                plane.Rotate(angleInRadians, plane.XAxis);

                return plane;
            }

            // Method to get planes at key points (start, end, and arbitrary points) on the curve
            public List<Plane> GenerateKeyPlanes(Curve curve, double t1, double t2, bool alignToCurve = true)
            {
                List<Plane> planes = new List<Plane>();

                // Generate planes at the start and end of the curve
                planes.Add(GeneratePlaneAt(curve, curve.Domain.Min, alignToCurve));
                planes.Add(GeneratePlaneAt(curve, curve.Domain.Max, alignToCurve));

                // Generate planes at custom parameters t1 and t2
                planes.Add(GeneratePlaneAt(curve, t1, alignToCurve));
                planes.Add(GeneratePlaneAt(curve, t2, alignToCurve));

                return planes;
            }
        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {   
            var PlaneGenerator = new PlaneGenerator();

            // Declare a list to store the planes
            List<Plane> planes = new List<Plane>();

            // Declare a list to store the curves
            List<Curve> curves = new List<Curve>();

            // Declare a variable to store the max rotation angle
            double maxRotationAngle = 0.0;

            double t1 = 0.25;
            double t2 = 0.75;


            // Retrieve the input data
            if (!DA.GetDataList(0, curves)) return;
            if (!DA.GetData(1, ref maxRotationAngle)) return;
            if (!DA.GetData(2, ref t1)) return;


            // Iterate over the curves
            foreach (Curve curve in curves)
            {
                // Generate key planes on the curve
                List<Plane> keyPlanes = PlaneGenerator.GenerateKeyPlanes(curve, 0.25, 0.75);

                // Rotate the planes around their X-axis
                for (int i = 0; i < keyPlanes.Count; i++)
                {
                    Plane rotatedPlane = PlaneGenerator.RotatePlaneAroundX(keyPlanes[i], 25.0, maxRotationAngle);
                    planes.Add(rotatedPlane);
                }
            }

            // Set the output data
            DA.SetDataList(0, planes);
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
            get { return new Guid("731C0911-B8D5-4B1A-B50C-AFE69E297DD0"); }
        }
    }
}