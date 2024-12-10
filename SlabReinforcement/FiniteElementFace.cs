using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlabReinforcement
{
    public class FiniteElementFace
    {
        public int Id { get; set; }
        public List<XYZ> Vertices { get; set; }
        public Color FaceColor { get; set; }
        public RebarBarType RebarType { get; set; }
        public double Spacing { get; set; }

        public FiniteElementFace(int id, List<XYZ> vertices, Color faceColor)
        {
            Id = id;
            Vertices = vertices;
            FaceColor = faceColor;
        }

        // Свойство для вычисления центроида
        public XYZ Centroid
        {
            get
            {
                double x = Vertices.Average(v => v.X);
                double y = Vertices.Average(v => v.Y);
                double z = Vertices.Average(v => v.Z);
                return new XYZ(x, y, z);
            }
        }
    }

}
