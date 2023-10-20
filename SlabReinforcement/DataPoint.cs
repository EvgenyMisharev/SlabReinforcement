using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SlabReinforcement
{
    public class DataPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public Color Color { get; set; }

        public DataPoint(double x, double y, Color color)
        {
            X = x;
            Y = y;
            Color = color;
        }
    }
}
