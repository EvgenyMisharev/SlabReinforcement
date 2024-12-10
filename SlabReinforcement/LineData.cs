using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlabReinforcement
{
    public class LineData
    {
        public RebarBarType RebarType { get; set; }
        public double Spacing { get; set; }
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }

        public List<FiniteElementFace> Faces { get; set; }
    }
}
