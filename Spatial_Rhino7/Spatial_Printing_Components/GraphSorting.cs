using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Spatial_Rhino7.Spatial_Printing_Components
{
    public class GraphSorting : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CurvePlaneGenerator class.
        /// </summary>
        public GraphSorting()
          : base("Graph Sorting", "GS",
              "Generates a graph from the spatial toolpaths and sorts to find a collision free robotic toolpath",
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
            pManager.AddLineParameter("Graph_Lines", "GL", "Graph:  Lines", GH_ParamAccess.tree);
            pManager.AddPointParameter("Graph_Nodes", "GN", "Graph: Nodes", GH_ParamAccess.tree);
            pManager.AddPointParameter("Graph_Node_Connections", "GNC", "Graph: Graph Node Connectivity", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Graph_Node_Connections_Length", "GNCL", "Graph: Graph Node Connectivity Count Per Node", GH_ParamAccess.tree);

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        public class Graph
        {
            // Dictionary to store nodes and their connections
            private Dictionary<Point3d, HashSet<Point3d>> nodes;

            public Graph()
            {
                nodes = new Dictionary<Point3d, HashSet<Point3d>>();
            }

            // Add a node to the graph
            public void AddNode(Point3d node)
            {
                if (!nodes.ContainsKey(node))
                {
                    nodes[node] = new HashSet<Point3d>(); // Initialize an empty set for connections
                }
            }

            // Add an edge between two nodes
            public void AddEdge(Point3d node1, Point3d node2)
            {
                AddNode(node1);
                AddNode(node2);
                nodes[node1].Add(node2);
                nodes[node2].Add(node1);
            }

            // Create graph from lines
            public void CreateGraphFromLines(IEnumerable<Line> lines)
            {
                foreach (var line in lines)
                {
                    Point3d startPoint = line.From;
                    Point3d endPoint = line.To;
                    AddEdge(startPoint, endPoint);
                }
            }

            // Get the nodes and their connections
            public Dictionary<Point3d, HashSet<Point3d>> GetNodes()
            {
                return nodes;
            }
        }

        public class LineProcessor
        {
            public static Dictionary<Line, double> AddWeightToLines(List<Line> graphLines)
            {
                if (graphLines == null || graphLines.Count == 0)
                    throw new ArgumentException("Input list of lines is empty");

                Dictionary<Line, double> weights = new Dictionary<Line, double>();

                foreach (var line in graphLines)
                {
                    Point3d sp = line.From;
                    Point3d ep = line.To;

                    // Ensure line direction is correct
                    if (sp.Z > ep.Z)
                        line.Flip();

                    // Calculate average Z height
                    double averageZ = (sp.Z + ep.Z) / 2;
                    double weight = 0;

                    if (Math.Abs(sp.Z - ep.Z) < 0.01)
                    {
                        // Horizontal line
                        // No weight changes for horizontal lines
                    }
                    else if (Math.Round(sp.X, 2) == Math.Round(ep.X, 2) &&
                             Math.Round(sp.Y, 2) == Math.Round(ep.Y, 2))
                    {
                        // Vertical line
                        weight += FindIntersectionVerticalAtEnd(line, graphLines);
                        weight += FindIntersectionVerticalAtStart(line, graphLines);
                        weight += VerticalNoAngleAtTop(line, graphLines);
                    }
                    else
                    {
                        // Angled line
                        weight += FindIntersectionAngledAtStart(line, graphLines);
                        weight += FindIntersectionAngledAtEnd(line, graphLines);
                        weight += VerticalsWithAngledAtStart(line, graphLines);
                    }

                    // Apply final weight adjustment
                    weight += CalculateWeight(averageZ) - 32.481;
                    weight = Math.Round(weight, 3);

                    weights[line] = weight;
                }

                return weights;
            }

            private static double FindIntersectionVerticalAtStart(Line line, List<Line> graphLines)
            {
                // Intersection logic for vertical lines at the start
                return 0.1; // Placeholder logic
            }

            private static double FindIntersectionVerticalAtEnd(Line line, List<Line> graphLines)
            {
                // Intersection logic for vertical lines at the end
                return 0.05; // Placeholder logic
            }

            private static double FindIntersectionAngledAtStart(Line line, List<Line> graphLines)
            {
                // Intersection logic for angled lines at the start
                return -0.15; // Placeholder logic
            }

            private static double FindIntersectionAngledAtEnd(Line line, List<Line> graphLines)
            {
                // Intersection logic for angled lines at the end
                return 0.21; // Placeholder logic
            }

            private static double VerticalNoAngleAtTop(Line inputLine, List<Line> graphLines)
            {
                // Logic for no-angle vertical lines
                Point3d startPoint = inputLine.From;
                Point3d endPoint = inputLine.To;

                double averageZInputLine = (startPoint.Z + endPoint.Z) / 2;
                double weightVertical = 0;

                // Ensure line direction is correct
                if (startPoint.Z > endPoint.Z)
                {
                    inputLine = new Line(endPoint, startPoint);
                }

                int count = 0;

                foreach (var line in graphLines)
                {
                    if (Math.Abs(line.To.Z - line.From.Z) <= 0.02)
                    {
                        // Horizontal Line
                        weightVertical = 0;
                    }
                    else if (line.From.X == line.To.X && line.From.Y == line.To.Y)
                    {
                        // Vertical Line with no angle
                        weightVertical = 0;
                    }
                    else
                    {
                        if (line.From.Z > line.To.Z)
                        {
                            line.Flip();
                        }

                        double averageZLine = (line.From.Z + line.To.Z) / 2;

                        // Intersection at the endpoint
                        if (averageZLine <= averageZInputLine)
                        {
                            if (Math.Round(line.To.X, 2) == Math.Round(endPoint.X, 2) &&
                                Math.Round(line.To.Y, 2) == Math.Round(endPoint.Y, 2) &&
                                Math.Round(line.To.Z, 2) == Math.Round(endPoint.Z, 2))
                            {
                                count++;
                            }
                        }
                    }
                }

                if (count == 0)
                {
                    weightVertical = 0.07;
                }

                return weightVertical;
            }

            private static double VerticalsWithAngledAtStart(Line line, List<Line> graphLines)
            {
                // Logic for verticals with angles at the start
                return 0.07; // Placeholder logic
            }

            private static double CalculateWeight(double averageZ)
            {
                // Weight calculation logic
                return averageZ; // Placeholder logic
            }
        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Declare List of curves
            List<Curve> curves = new List<Curve>();

            // Retrieve the input data
            if (!DA.GetDataList(0, curves)) return;

            // Initialize a GH_Structure to store grafted data
            GH_Structure<GH_Point> graftedTree = new GH_Structure<GH_Point>();

            // 3. Abort on invalid inputs.
            if (curves == null)
            {
                RhinoApp.WriteLine("The selected object is not a curve.");
            }
            //convert curves to lines
            List<Line> lines = new List<Line>();
            foreach (var curve in curves) {
                Polyline polyline;
                curve.TryGetPolyline(out polyline);
                for (int i = 0; i < polyline.Count - 1; i++)
                {
                    lines.Add(new Line(polyline[i], polyline[i + 1]));
                }
            }

            // Initialize a spatial graph
            Graph spatialGraph = new Graph();


            // Create the graph from lines
            spatialGraph.CreateGraphFromLines(lines);

            // Retrieve nodes and their connections
            var nodes = spatialGraph.GetNodes();

            // Collect nodes and connections for further processing if needed
            List<Point3d> nodeList = new List<Point3d>();
            List<Point3d> nodeConnections = new List<Point3d>();
            List<int> connectionLengths = new List<int>();
            int counter = 0;

            foreach (var node in nodes)
            {
                nodeList.Add(node.Key);
                connectionLengths.Add(node.Value.Count);
                GH_Path path = new GH_Path(counter);

                foreach (var connection in node.Value)
                {
                    nodeConnections.Add(connection);
                    
                    graftedTree.Append(new GH_Point(connection), path);
                }
                counter++;
            }

            // Set the output data
            DA.SetDataList(0, lines);
            DA.SetDataList(1, nodeList);
            DA.SetDataTree(2, graftedTree);
            DA.SetDataList(3, connectionLengths);
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
            get { return new Guid("f87048ef-6762-48bc-86f9-d91e27b0ebd5"); }
        }
    }
}