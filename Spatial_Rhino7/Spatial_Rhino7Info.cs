using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace Spatial_Rhino7
{
    public class Spatial_Rhino7Info : GH_AssemblyInfo
    {
        public override string Name => "Spatial_Rhino7";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("d37971b4-f665-4119-98ba-beeab1623e3d");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}