using ILNumerics.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomChopper.Geom
{
    public class Geometry
    {
        public static double Area(NumSharp.NDArray pointSet)
        {
            int d = pointSet.size / 3;
            double area = 0;
            for (int i = 0; i < d-1; i++)
            {
                area += pointSet[i, 0] * pointSet[i + 1, 1] - pointSet[i + 1, 0] * pointSet[i, 1];
            }
            area += pointSet[d - 1, 0] * pointSet[0, 1] - pointSet[0, 0] * pointSet[d - 1, 1];
            return 0.5*Math.Abs(area);

        }
        public static double Area(double[,] pointSet)
        {
            int d = pointSet.Length / 3;
            double area = 0;
            for (int i = 0; i < d - 1; i++)
            {
                area += pointSet[i, 0] * pointSet[i + 1, 1] - pointSet[i + 1, 0] * pointSet[i, 1];
            }
            area += pointSet[d - 1, 0] * pointSet[0, 1] - pointSet[0, 0] * pointSet[d - 1, 1];
            return 0.5 * Math.Abs(area);

        }
        public static double Area(List<double[]> pointSet)
        {
            int d = pointSet.Count;
            double area = 0;
            for (int i = 0; i < d - 1; i++)
            {
                area += (pointSet[i][0] * pointSet[i + 1][1]) - (pointSet[i + 1][0] * pointSet[i][1]);
            }
            area += (pointSet[d - 1][0] * pointSet[0][1]) - (pointSet[0][0] * pointSet[d - 1][1]);
            return 0.5 * Math.Abs(area);

        }
    }
}
