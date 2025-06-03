using System;
using System.Collections.Generic;
using Rhino;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Collections;
using System.Linq;
using Rhino.ApplicationSettings;
using Grasshopper.Kernel.Data;
using Grasshopper;

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
            pManager.AddLineParameter("pathCurves", "pC", "a list of Curves", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Curve Orientation", "pC", "Resorted curves", GH_ParamAccess.list);
            pManager.AddTextParameter("Curve Connectivity", "uC", "Resorted curves in pairs", GH_ParamAccess.list);
            pManager.AddCurveParameter("Cluster Curves", "cC", "Resorted curves in pairs", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Cluster Vertical Angled Pairs", "CVAP", "Resorted curves in pairs", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Connected Angled Pairs", "CAP", "Continious Path of Vertical Angled Lines", GH_ParamAccess.tree);

        }

        public class SpatialPaths
        {
            public Dictionary<Guid, PathCurve> Paths { get; private set; } = new Dictionary<Guid, PathCurve>();
            public Dictionary<Guid, List<Guid>> CurveGraph { get; private set; } = new Dictionary<Guid, List<Guid>>();

            private double tolerance;

            public SpatialPaths(List<Line> lines, double tolerance = 1e-6)
            {
                this.tolerance = tolerance;

                foreach (var line in lines)
                {
                    var path = new PathCurve(line, tolerance);
                    Paths[path.Id] = path;
                }

                ComputeConnectivity();
                BuildGraph();
            }

            private void ComputeConnectivity()
            {
                foreach (var path in Paths.Values)
                {
                    path.StartConnections.Clear();
                    path.EndConnections.Clear();

                    foreach (var other in Paths.Values)
                    {
                        if (path.Id == other.Id) continue;

                        if (path.StartPoint.DistanceTo(other.StartPoint) < tolerance ||
                            path.StartPoint.DistanceTo(other.EndPoint) < tolerance)
                            path.StartConnections.Add(other.Id);

                        if (path.EndPoint.DistanceTo(other.StartPoint) < tolerance ||
                            path.EndPoint.DistanceTo(other.EndPoint) < tolerance)
                            path.EndConnections.Add(other.Id);
                    }
                }
            }

            private void BuildGraph()
            {
                CurveGraph.Clear();

                foreach (var path in Paths.Values)
                {
                    HashSet<Guid> neighbors = new HashSet<Guid>();
                    neighbors.UnionWith(path.StartConnections);
                    neighbors.UnionWith(path.EndConnections);

                    CurveGraph[path.Id] = neighbors.ToList();
                }
            }

            public Dictionary<string, int> GetOrientationStats()
            {
                return Paths.Values
                    .GroupBy(p => p.Orientation)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count());
            }

            /// <summary>
            /// Finds clusters of PathCurves based on connectivity.
            /// </summary>
            /// <returns></returns>
            public List<List<Guid>> FindClusters()
            {
                var visited = new HashSet<Guid>();
                var clusters = new List<List<Guid>>();

                foreach (var curveId in Paths.Keys)
                {
                    if (visited.Contains(curveId))
                        continue;

                    var cluster = new List<Guid>();
                    var stack = new Stack<Guid>();
                    stack.Push(curveId);

                    while (stack.Count > 0)
                    {
                        var current = stack.Pop();
                        if (visited.Contains(current)) continue;

                        visited.Add(current);
                        cluster.Add(current);

                        if (CurveGraph.ContainsKey(current))
                        {
                            foreach (var neighbor in CurveGraph[current])
                            {
                                if (!visited.Contains(neighbor))
                                    stack.Push(neighbor);
                            }
                        }
                    }

                    clusters.Add(cluster);
                }

                return clusters;
            }

            /// <summary>
            /// Finds clusters of PathCurves based on their Midpoint Z-coordinates, allowing for a specified tolerance.
            /// </summary>
            /// <param name="zTolerance"></param>
            /// <returns></returns>

            public List<List<Guid>> FindZLayeredClusters(double zTolerance = 1e-3)
            {
                var zGroups = GroupByZ(zTolerance);
                var allClusters = new List<List<Guid>>();

                foreach (var group in zGroups.Values)
                {
                    var visited = new HashSet<Guid>();

                    foreach (var curveId in group)
                    {
                        if (visited.Contains(curveId))
                            continue;

                        var cluster = new List<Guid>();
                        var stack = new Stack<Guid>();
                        stack.Push(curveId);

                        while (stack.Count > 0)
                        {
                            var current = stack.Pop();
                            if (visited.Contains(current)) continue;

                            visited.Add(current);
                            cluster.Add(current);

                            if (CurveGraph.ContainsKey(current))
                            {
                                foreach (var neighbor in CurveGraph[current])
                                {
                                    if (!visited.Contains(neighbor) && group.Contains(neighbor))
                                        stack.Push(neighbor);
                                }
                            }
                        }

                        allClusters.Add(cluster);
                    }
                }

                return allClusters;
            }

            /// <summary>
            /// Returns a list of clusters of PathCurves based on connectivity.
            /// </summary>
            /// <returns></returns>
            public List<List<Line>> GetCurveClusters()
            {
                //var idClusters = FindClusters();
                var idClusters = FindZLayeredClusters();

                return idClusters.Select(cluster => cluster.Select(id => Paths[id].Line).ToList()).ToList();
            }

            /// <summary>
            /// Groub by PathCurves based on their Midpoint Z-coordinate.
            /// </summary>
            /// <param name="zTolerance"></param>
            /// <returns></returns>
            private Dictionary<double, List<Guid>> GroupByZ(double zTolerance = 1e-3)
            {
                var zGroups = new Dictionary<double, List<Guid>>();

                foreach (var kvp in Paths)
                {
                    var z = kvp.Value.MidpointZ;
                    double zKey = Math.Round(z / zTolerance) * zTolerance;

                    if (!zGroups.ContainsKey(zKey))
                        zGroups[zKey] = new List<Guid>();

                    zGroups[zKey].Add(kvp.Key);
                }

                return zGroups;
            }

            public List<(PathCurve Vertical, PathCurve Angled)> FindVerticalAngledPairs(List<Guid> clusterIds, double tolerance = 1e-6)
            {
                var pairs = new List<(PathCurve, PathCurve)>();
                var usedVerticals = new HashSet<Guid>();
                var usedAngleds = new HashSet<Guid>();

                var verticals = clusterIds
                    .Select(id => Paths[id])
                    .Where(p => p.Orientation == PathCurve.OrientationType.Vertical)
                    .ToList();

                var angleds = clusterIds
                    .Select(id => Paths[id])
                    .Where(p => p.Orientation == PathCurve.OrientationType.Angled)
                    .ToList();

                foreach (var v in verticals)
                {
                    if (usedVerticals.Contains(v.Id)) continue;

                    foreach (var a in angleds)
                    {
                        if (usedAngleds.Contains(a.Id)) continue;

                        // Get all endpoints
                        var endpoints = new[] { v.StartPoint, v.EndPoint, a.StartPoint, a.EndPoint };

                        // Get highest Z point
                        var maxZPoint = endpoints.OrderByDescending(pt => pt.Z).First();

                        // Check if maxZPoint is shared between v and a
                        bool vHas = IsCloseTo(v.StartPoint, maxZPoint, tolerance) || IsCloseTo(v.EndPoint, maxZPoint, tolerance);
                        bool aHas = IsCloseTo(a.StartPoint, maxZPoint, tolerance) || IsCloseTo(a.EndPoint, maxZPoint, tolerance);

                        if (vHas && aHas)
                        {
                            pairs.Add((v, a));
                            usedVerticals.Add(v.Id);
                            usedAngleds.Add(a.Id);
                            break; // Move to next vertical
                        }
                    }
                }

                return pairs;
            }

            private bool IsCloseTo(Point3d a, Point3d b, double tolerance)
            {
                return a.DistanceTo(b) < tolerance;
            }

            public List<Polyline> BuildLongestChains(List<(PathCurve Vertical, PathCurve Angled)> pairs, double tolerance = 1e-6)
            {

                bool expectVerticalAtTail = true; // tail starts as Angled → next must be Vertical
                bool expectAngledAtHead = true;  // head starts as Vertical → next must be Angled

                var unused = new HashSet<int>(Enumerable.Range(0, pairs.Count));
                var polylines = new List<Polyline>();
                var pairPolys = pairs.Select(p => new PairPolyline(p.Vertical, p.Angled)).ToList();

                while (unused.Count > 0)
                {
                    int startIdx = unused.First();
                    unused.Remove(startIdx);

                    var chainPts = new List<Point3d>(pairPolys[startIdx].Polyline);
                    var used = new HashSet<int> { startIdx };

                    bool extended;

                    do
                    {
                        extended = false;
                        Point3d head = chainPts.First();
                        Point3d tail = chainPts.Last();

                        // Get orientation at both ends
                        bool headExpectAngled = true;  // since chain starts with vertical
                        bool tailExpectVertical = true; // since chain ends with angled

                        foreach (var i in unused.ToList())
                        {
                            var pp = pairPolys[i];

                            // Tail connection: must be Vertical
                            if (expectVerticalAtTail && pp.Vertical != null &&
                                (IsCloseTo(pp.Vertical.StartPoint, tail, tolerance) || IsCloseTo(pp.Vertical.EndPoint, tail, tolerance)))
                            {
                                chainPts.AddRange(pp.Polyline.Skip(1));
                                unused.Remove(i);
                                expectVerticalAtTail = false; // next one would be angled
                                expectAngledAtHead = true;
                                extended = true;
                                break;
                            }

                            // Head connection: must be Angled
                            if (expectAngledAtHead && pp.Angled != null &&
                                (IsCloseTo(pp.Angled.StartPoint, head, tolerance) || IsCloseTo(pp.Angled.EndPoint, head, tolerance)))
                            {
                                var reversed = new List<Point3d>(pp.Polyline);
                                reversed.Reverse();
                                chainPts.InsertRange(0, reversed.Skip(1));
                                unused.Remove(i);
                                expectAngledAtHead = false; // next one would be vertical
                                expectVerticalAtTail = true;
                                extended = true;
                                break;
                            }
                        }

                    } while (extended);

                    polylines.Add(new Polyline(chainPts));
                }

                return polylines;
            }

            public List<List<Polyline>> BuildClusteredChains(List<List<Guid>> clusters, double tolerance = 1e-6)
            {
                var allChains = new List<List<Polyline>>();

                foreach (var cluster in clusters)
                {
                    var pairs = FindVerticalAngledPairs(cluster, tolerance);
                    var chains = BuildLongestChains(pairs, tolerance);
                    allChains.Add(chains);
                }

                return allChains;
            }
        }

        public class PathCurve
        {
            public Guid Id { get; }
            public Line Line { get; }
            public Point3d StartPoint => Line.From;
            public Point3d EndPoint => Line.To;
            public Point3d MidPoint => Line.PointAt(0.5);
            public double MidpointZ => MidPoint.Z;
            public OrientationType Orientation { get; }


            public List<Guid> StartConnections { get; set; } = new List<Guid>();
            public List<Guid> EndConnections { get; set; } = new List<Guid>();

            public PathCurve(Line line, double tolerance)
            {
                Id = Guid.NewGuid();
                Line = line;
                Orientation = ComputeOrientation(tolerance);
            }

            private OrientationType ComputeOrientation(double tolerance)
            {
                Vector3d dir = Line.Direction;
                dir.Unitize();

                if (Math.Abs(dir.X) < tolerance && Math.Abs(dir.Y) < tolerance && Math.Abs(dir.Z) > tolerance)
                    return OrientationType.Vertical;

                if (Math.Abs(dir.Z) < tolerance)
                    return OrientationType.Horizontal;

                return OrientationType.Angled;
            }

            public enum OrientationType
            {
                Vertical,
                Horizontal,
                Angled
            }
        }

        public class PairPolyline
        {
            public PathCurve Vertical { get; }
            public PathCurve Angled { get; }
            public Polyline Polyline { get; }

            public Point3d Start => Polyline.First;
            public Point3d End => Polyline.Last;

            public PairPolyline(PathCurve v, PathCurve a)
            {
                Vertical = v;
                Angled = a;

                var pts = new List<Point3d>
        {
            v.Line.From, v.Line.To,
            a.Line.To.DistanceTo(v.Line.To) < a.Line.From.DistanceTo(v.Line.To)
                ? a.Line.From
                : a.Line.To
        };

                Polyline = new Polyline(pts);
            }

            public bool StartsWithVertical => true; // enforced by definition
            public bool EndsWithAngled => true;     // enforced by order
        }

        public List<List<(PathCurve Vertical, PathCurve Angled)>> SortConnectedPairChains(
    List<(PathCurve Vertical, PathCurve Angled)> pairs,
    double tolerance = 1e-6)
        {
            var chains = new List<List<(PathCurve, PathCurve)>>();
            var unused = new HashSet<int>(Enumerable.Range(0, pairs.Count));

            // Build a lookup of endpoints → pair index
            var endpointMap = new Dictionary<Point3d, List<int>>(new Point3dComparer(tolerance));
            for (int i = 0; i < pairs.Count; i++)
            {
                var v = pairs[i].Vertical;
                var a = pairs[i].Angled;
                var pts = new[] { v.StartPoint, v.EndPoint, a.StartPoint, a.EndPoint };

                foreach (var pt in pts)
                {
                    if (!endpointMap.ContainsKey(pt))
                        endpointMap[pt] = new List<int>();
                    endpointMap[pt].Add(i);
                }
            }

            while (unused.Count > 0)
            {
                int seed = unused.First();
                unused.Remove(seed);

                var chain = new List<(PathCurve, PathCurve)> { pairs[seed] };
                var front = GetEndpoints(pairs[seed]);
                bool extended;

                do
                {
                    extended = false;

                    // Try to extend at the end
                    Point3d tail = front.tail;
                    if (endpointMap.TryGetValue(tail, out var candidates))
                    {
                        foreach (int c in candidates)
                        {
                            if (!unused.Contains(c)) continue;
                            var (v2, a2) = pairs[c];
                            var ends = GetEndpoints(pairs[c]);
                            if (IsSamePoint(ends.head, tail, tolerance) || IsSamePoint(ends.tail, tail, tolerance))
                            {
                                chain.Add(pairs[c]);
                                unused.Remove(c);
                                front = GetEndpoints(pairs[c]);
                                extended = true;
                                break;
                            }
                        }
                    }

                    // Try to extend at the start
                    if (!extended)
                    {
                        Point3d head = GetEndpoints(chain[0]).head;
                        if (endpointMap.TryGetValue(head, out candidates))
                        {
                            foreach (int c in candidates)
                            {
                                if (!unused.Contains(c)) continue;
                                var (v2, a2) = pairs[c];
                                var ends = GetEndpoints(pairs[c]);
                                if (IsSamePoint(ends.head, head, tolerance) || IsSamePoint(ends.tail, head, tolerance))
                                {
                                    chain.Insert(0, pairs[c]);
                                    unused.Remove(c);
                                    extended = true;
                                    break;
                                }
                            }
                        }
                    }

                } while (extended);

                chains.Add(chain);
            }

            return chains;
        }

        private (Point3d head, Point3d tail) GetEndpoints((PathCurve Vertical, PathCurve Angled) pair)
        {
            var shared = GetSharedEndpoint(pair.Vertical.Line, pair.Angled.Line, 1e-6);
            Point3d otherV = shared.DistanceTo(pair.Vertical.StartPoint) < 1e-6 ? pair.Vertical.EndPoint : pair.Vertical.StartPoint;
            Point3d otherA = shared.DistanceTo(pair.Angled.StartPoint) < 1e-6 ? pair.Angled.EndPoint : pair.Angled.StartPoint;
            return (otherV, otherA); // head = vertical base, tail = angled tip
        }

        private Point3d GetSharedEndpoint(Line l1, Line l2, double tol)
        {
            foreach (var p1 in new[] { l1.From, l1.To })
                foreach (var p2 in new[] { l2.From, l2.To })
                    if (p1.DistanceTo(p2) < tol)
                        return p1;

            throw new Exception("No shared point found");
        }

        //private bool IsSamePoint(Point3d a, Point3d b, double tol) => a.DistanceTo(b) < tol;

        class Point3dComparer : IEqualityComparer<Point3d>
        {
            private readonly double _tolerance;
            public Point3dComparer(double tol) => _tolerance = tol;

            public bool Equals(Point3d a, Point3d b) => a.DistanceTo(b) < _tolerance;

            public int GetHashCode(Point3d pt)
            {
                return (int)(pt.X / _tolerance) ^
                       (int)(pt.Y / _tolerance) ^
                       (int)(pt.Z / _tolerance);
            }
        }

        private bool IsSamePoint(Point3d a, Point3d b, double tol)
        {
            return a.DistanceTo(b) < tol;
        }


        private DataTree<Curve> ConvertToTree(List<List<Line>> clusters)
        {
            var tree = new DataTree<Curve>();
            for (int i = 0; i < clusters.Count; i++)
            {
                GH_Path path = new GH_Path(i);
                foreach (var line in clusters[i])
                {
                    tree.Add(new LineCurve(line), path); // Convert Line → Curve
                }
            }
            return tree;
        }

        public DataTree<Curve> ConvertPairChainsToTree(List<List<(PathCurve Vertical, PathCurve Angled)>> chains)
        {
            var tree = new DataTree<Curve>();

            for (int i = 0; i < chains.Count; i++)
            {
                GH_Path path = new GH_Path(i);

                foreach (var (v, a) in chains[i])
                {
                    tree.Add(new LineCurve(v.Line), path);
                    tree.Add(new LineCurve(a.Line), path);
                }
            }

            return tree;
        }


        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Line> pathCurves = new List<Line>();
            List<string> orientation = new List<string>();
            List<string> connectivity = new List<string>();



            if (!DA.GetDataList(0, pathCurves)) { return; }

            List<Line> inputLines = pathCurves;
            var spatialPaths = new SpatialPaths(inputLines);

            var stats = spatialPaths.GetOrientationStats();
            foreach (var kvp in stats)
                //Rhino.RhinoApp.WriteLine($"{kvp.Key}: {kvp.Value}");
                orientation.Add($"{kvp.Key}: {kvp.Value}");
                

            foreach (var path in spatialPaths.Paths.Values)
            {
                connectivity.Add($"Curve {path.Id} has {path.StartConnections.Count} connections at start, " +
                                         $"{path.EndConnections.Count} at end. Orientation: {path.Orientation}");
            }

            var clusters = spatialPaths.GetCurveClusters();
            DataTree<Curve> clusterTree = ConvertToTree(clusters);
            var VAclusters = spatialPaths.FindZLayeredClusters();

            

            foreach (var cluster in VAclusters)
            {
                var pairs = spatialPaths.FindVerticalAngledPairs(cluster);
                Rhino.RhinoApp.WriteLine($"Cluster has {pairs.Count} vertical-angled pairs");
                var chains = spatialPaths.BuildLongestChains(pairs);
            }

            // Build chains from the pairs
            var allChains = new List<List<(PathCurve, PathCurve)>>();

            foreach (var cluster in VAclusters)
            {
                var pairs = spatialPaths.FindVerticalAngledPairs(cluster);
                var chains = SortConnectedPairChains(pairs); // The method we just wrote
                allChains.AddRange(chains);
            }

            var tree = ConvertPairChainsToTree(allChains);

            var verticalTree = new DataTree<Curve>();
            var angledTree = new DataTree<Curve>();
            var pairsTree = new DataTree<Curve>();

            int pairIndex = 0;

            foreach (var cluster in VAclusters)
            {
                var pairs = spatialPaths.FindVerticalAngledPairs(cluster);
                var chains = SortConnectedPairChains(pairs);

                for (int i = 0; i < chains.Count; i++)
                {
                    RhinoApp.WriteLine($"Chain {i}: {chains[i].Count} pairs");
                }


                foreach (var (vertical, angled) in pairs)
                {
                    var path = new GH_Path(pairIndex++);
                    pairsTree.Add(new LineCurve(vertical.Line), path);
                    pairsTree.Add(new LineCurve(angled.Line), path);
                }

            }



            // 3. Set the outputs
            DA.SetDataList(0, orientation);
            DA.SetDataList(1, connectivity);
            DA.SetDataTree(2, clusterTree);
            DA.SetDataTree(3, pairsTree);
            DA.SetDataTree(4, tree);


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