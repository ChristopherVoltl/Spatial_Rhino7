using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Grasshopper.Kernel;
using Rhino.Geometry;

public class N_Bracing : GH_Component
{
    /// <summary>
    /// Initializes a new instance of the CurvePlaneGenerator class.
    /// </summary>
    public N_Bracing()
      : base("N - bracing", "N",
          "Generates spatial toolpaths for N Bracing type spatial structures",
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
        pManager.AddPlaneParameter("Planes", "PL", "Planes created in respects to the robotic tilt angle", GH_ParamAccess.list);
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        Curve[] pathCurves = null;
            
        
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