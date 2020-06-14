using ILNumerics.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
        public static bool PointInPolygon(double[,] polygon, double[] point)
        {
            bool result = false;
            int j = polygon.GetLength(0) -1;
            for (int i = 0; i < polygon.GetLength(0); i++)
            {
                if (((polygon[i,1]< point[1])&&(polygon[j,1] >= point[1]))|| ((polygon[i, 1] >= point[1]) && (polygon[j, 1] < point[1])))
                {
                    if (polygon[i,0] + (point[1] - polygon[i,1]) / (polygon[j,1] - polygon[i,1])*(polygon[j,0] - polygon[i,0]) < point[0])
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;

        }
        
        public static int ClosestPoint(double x, double y, double[,] points)
        {
            double m = 1000;
            int closestPoint = 1000;
            for (int i = 0; i < points.Length / 3; i++)
            {
                double diff = Math.Sqrt(Math.Pow((x - points[i, 0]), 2) + Math.Pow((y - points[i, 1]), 2));    //difference between points in xy plane
                if (diff < m)
                {
                    closestPoint = i;
                    m = diff;
                }
            }
            if (closestPoint == 1000)
            {
                Console.WriteLine("Closest Point not found, terminating.");
                Thread.Sleep(2000);    //pause for 2 seconds
                System.Environment.Exit(0);
            }
            return closestPoint;
        }
        public static double[] InterpolateXY(double[] point1, double[] point2, double z)
        {
            double xSlope = (point2[0] - point1[0]) / (point2[2] - point1[2]);
            double ySlope = (point2[1] - point1[1]) / (point2[2] - point1[2]);

            double newX = point1[0] + xSlope * (z - point1[2]);
            double newY = point1[1] + ySlope * (z - point1[2]);

            double[] newPoint = new double[3] { newX, newY, z };
            return newPoint;

        }
        public static double[,] InterpBetweenContours(double[,] a, double[,] b, double zVal)
            //interpolate between contours a and b at the specified z value. (z is between the contours)
        {
            //First check if either contour is at zVal exactly: 
            if (a[0,2] == zVal)
            {
                return a;
            }else if (b[0,2] == zVal)
            {
                return b;
            }

            double x, y, z;
            double[,] c = new double[a.GetLength(0), 2];

            for (int i = 0; i < a.GetLength(0); i++)
            {
                x = a[i, 0];
                y = a[i, 1];
                z = a[i, 2];
                //Now get idx, the row index in the second closest contour. Then interpolate between the two.
                int idx = ClosestPoint(x, y, b);
                double[] point1 = { x, y, z };
                double[] point2 = {b[idx,0], b[idx,1], b[idx,2] };
                double[] newPoint = InterpolateXY(point1, point2, zVal);
                c[i, 0] = newPoint[0];
                c[i, 1] = newPoint[1];
                c[i, 2] = newPoint[2];
            }
            return c;
        }   
    }
}
