using Dbscan;

namespace SlabReinforcement
{
    public class SimplePoint : IPointData
    {
        public SimplePoint(double x, double y) =>
        Point = new Point(x, y);

        public Point Point { get; }
    }
}
