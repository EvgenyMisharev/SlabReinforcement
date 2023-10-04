using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlabReinforcement
{
    public class PointColorItemComparer : IEqualityComparer<PointColorItem>
    {
        public bool Equals(PointColorItem x, PointColorItem y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x == null || y == null)
                return false;

            // Сравниваем координаты точек и цвета
            return x.Point.Equals(y.Point) && x.Color.Equals(y.Color);
        }

        public int GetHashCode(PointColorItem obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            // Генерируем хеш-код на основе координат точки и цвета
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + obj.Point.GetHashCode();
                hash = hash * 23 + obj.Color.GetHashCode();
                return hash;
            }
        }
    }
}
