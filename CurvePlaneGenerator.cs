using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace CurveHelperFunctions
{
    public class CurvePlaneGenerator
    {
        public CurvePlaneGenerator(Curve curve, double maxRotationAngle = 45.0)
        {
            _curve = curve;
            _maxRotationAngle = maxRotationAngle;
        }

        // Method to generate a plane at a specific parameter along the curve
        public Plane GeneratePlaneAt(double t, bool alignToCurve = true)
        {
            if (!_curve.IsValid || !_curve.Domain.IncludesParameter(t))
                throw new ArgumentException("Invalid parameter on the curve");

            // Find the point and tangent at parameter t
            Point3d point = _curve.PointAt(t);
            Vector3d tangent = _curve.TangentAt(t);

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
        public Plane RotatePlaneAroundX(Plane plane, double rotationAngle)
        {
            // Constrain rotation angle to maxRotationAngle
            double clampedAngle = Math.Min(rotationAngle, _maxRotationAngle);
            double angleInRadians = RhinoMath.ToRadians(clampedAngle);

            // Perform rotation around the plane's X-axis
            plane.Rotate(angleInRadians, plane.XAxis);

            return plane;
        }

        // Method to get planes at key points (start, end, and arbitrary points) on the curve
        public List<Plane> GenerateKeyPlanes(double t1, double t2, bool alignToCurve = true)
        {
            List<Plane> planes = new List<Plane>();

            // Generate planes at the start and end of the curve
            planes.Add(GeneratePlaneAt(_curve.Domain.Min, alignToCurve));
            planes.Add(GeneratePlaneAt(_curve.Domain.Max, alignToCurve));

            // Generate planes at custom parameters t1 and t2
            planes.Add(GeneratePlaneAt(t1, alignToCurve));
            planes.Add(GeneratePlaneAt(t2, alignToCurve));

            return planes;
        }
    }
}