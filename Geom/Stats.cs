using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomChopper.Geometry
{
    public class Stats
    {
        public static double SliceMean(int dimension, double[,] points)
        {
            double totalSum = 0;
            double numPoints = 0;
            for (int i = 0; i < points.Length / 3; i++)
            {
                totalSum += points[i, dimension];
                numPoints++;
            }
            return totalSum / numPoints;
        }
        public static double SliceMean(int dimension, List<double[,]> points)
        {
            double totalSum = 0;
            double numPoints = 0;
            for (int j = 0; j < points.Count; j++)
            {
                for (int i = 0; i < points[j].Length / 3; i++)
                {
                    totalSum += points[j][i, dimension];
                    numPoints++;
                }
            }
            return totalSum / numPoints;
        }
        public static int[] LowestValIndices(double[,] a, int dim, int numberPoints)
            //returns a list of indices corresponding to the lowest values within a list at a certain dimension.
        {
            
        }
    }
}
