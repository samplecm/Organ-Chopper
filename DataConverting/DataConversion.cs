using NumSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomChopper.DataConverting
{
    class DataConversion
    {
        public static List<double[,]> NumSharpToArray(List<NDArray> contours)
        {
          
            int numConts = contours.Count;
            List<double[,]> newContours = new List<double[,]>();

            for (int i = 0; i < numConts; i++)
            {

                int size = contours[i].size;
                int row = 0;
                double[,] cont = new double[size / 3, 3];
                for (int j = 0; j < size; j++)
                {
                    int column = j % 3;

                    cont[row, column] = contours[i][row,column];

                    if (column == 2)
                    {
                        row++;
                    }

                }
                newContours.Add(cont);
            }
            return newContours;
        }
        public static double[] ReverseArray(double[] array)
        {
            double[] newArray = new double[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                newArray[i] = array[array.Length - 1 - i];
            }
            return newArray;
        }
    }
}
