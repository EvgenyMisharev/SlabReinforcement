using Dbscan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlabReinforcement
{
    public class SimplePoint : IPointData
    {
        public SimplePoint(double x, double y) =>
        Point = new Point(x, y);

        public Point Point { get; }
    }
}
