using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlabReinforcement
{
    public class Cluster
    {
        public RebarBarType RebarType { get; set; }
        public double Spacing { get; set; }
        public double MinX { get; set; } 
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public List<LineData> Lines { get; set; } = new List<LineData>();
    }
}
