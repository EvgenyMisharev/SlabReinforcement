using Autodesk.Revit.DB;

namespace SlabReinforcement
{
    public class PointColorItem
    {
        public XYZ Point;
        public Color Color;
        public PointColorItem(XYZ point, Color color)
        {
            Point = point;
            Color = color;
        }
    }
}
