using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DicomChopper.DataConverting;
using OpenTK.Graphics.OpenGL;

namespace DicomChopper.Geom
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
            double[] min = { a[0, dim], 0 };    //first entry min, second corresponding index
            List<int> used = new List<int>();
            int[] smalls = new int[numberPoints];

            for (int i = 0; i < numberPoints; i++)
            {
                for (int j = 0; j < a.GetLength(0); j++) {
                    if ((a[j, dim] < min[0])&&(!used.Contains(j)))
                    {
                        min[0] =  a[j, dim];
                        min[1] = j;
                    }
                }
                smalls[i] = (int)min[1];
                used.Add(smalls[i]);
                min[0] = 1000;
                min[1] = 0;
            }
            smalls = Sort(smalls);
            smalls = DataConversion.ReverseArray(smalls);
            return smalls;
        }
        public static int[] Sort(int[] a)
        {
            int[] b = new int[a.Length];
            int temp;
            for (int i = 0; i < a.Length - 1; i++)
            {
                for (int j = i + 1; j < a.Length; j++)
                {
                    if (a[i] > a[j])
                    {
                        temp = a[i];
                        a[i] = a[j];
                        a[j] = temp;
                    }
                }
            }
            return a;
        }
        public static double[,] RemovePoint(double[,] a, int index)
        //Add the first row to the end of an array. (close loops)
        {
            double[,] b = new double[a.Length / 3 - 1, 3];
            int row = 0;
            if (index != 0)
            {
                for (int j = 0; j < index * 3; j++)
                {
                    int column = j % 3;

                    b[row, column] = a[row, column];

                    if (column == 2)
                    {
                        row++;
                    }
                }
            }
            if (index != a.Length / 3)
            {
                row = index;
                for (int j = index * 3; j < b.Length - 3; j++)
                {
                    int column = j % 3;

                    b[row, column] = a[row + 1, column];

                    if (column == 2)
                    {
                        row++;
                    }
                }
            }

            return b;
        }
        public static int[,] RemovePoint(int[,] a, int index)
        //Add the first row to the end of an array. (close loops)
        {
            int[,] b = new int[a.Length / 3 - 1, 3];
            int row = 0;
            if (index != 0)
            {
                for (int j = 0; j < index * 3; j++)
                {
                    int column = j % 3;

                    b[row, column] = a[row, column];

                    if (column == 2)
                    {
                        row++;
                    }
                }
            }
            if (index != a.Length / 3)
            {
                row = index + 1;
                for (int j = index * 3; j < b.Length - 3; j++)
                {
                    int column = j % 3;

                    b[row, column] = a[row + 1, column];

                    if (column == 2)
                    {
                        row++;
                    }
                }
            }

            return b;
        }
        public static int[] RemovePoint(int[] a, int index)
        //Add the first row to the end of an array. (close loops)
        {
            int[] b = new int[a.Length - 1];
            int row = 0;
            if (index != 0)
            {
                for (int j = 0; j < index; j++)
                {
                    b[j] = a[j];
                }
            }
            if (index != a.Length)
            {
                for (int j = index; j < b.Length; j++)
                {                 
                    b[j] = a[j + 1];
                }
            }

            return b;
        }
        public static double min(double[,] a, int dim)
        {
            double min = 1000;
            for (int point = 0; point < a.GetLength(0); point++)
            {
                if (a[point, dim] < min)
                {
                    min = a[point, dim];
                }
            }
            return min;
        }
        public static double max(double[,] a, int dim)
        {
            double max = -1000;
            for (int point = 0; point < a.GetLength(0); point++)
            {
                if (a[point, dim] > max)
                {
                    max = a[point, dim];
                }
            }
            return max;
        }
    }
}
