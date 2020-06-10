using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using NumSharp;
using Dicom.IO.Buffer;

namespace DicomChopper.Segmentation
{
    class ContourFixing
    {
        public static List<NumSharp.NDArray> IslandRemover(List<NumSharp.NDArray> contours)
        //This function will remove ROI contour islands if they exist.
        //Basic idea is to search through contour points, and if there is a large
        //variation in x or y from one contour to the next, then remove the contour which is 
        //furthest from the mean.
        {
            Console.WriteLine("Checking for islands and removing");
            int numIslands = 0;
            int numContours = contours.Count;
            double meanX = 0;
            double meanY = 0;
            int maxSep = 30; //island cutoff criteria (mm), difference in means between adjacent contours (X,y)
            List<double> means = new List<double>();
            int numPoints = 0; //divide by to get mean.


            //first get the mean x,y,z for the whole ROI:
            for (int i = 0; i < numContours; i++)
            {
                meanX += np.mean(contours[i][Slice.All, 0]) * contours[i].size / 3;
                meanY += np.mean(contours[i][Slice.All, 1]) * contours[i].size / 3; ///3 because just want # per column.
                numPoints += contours[i].size / 3;
            }
            meanX /= numPoints;
            meanY /= numPoints;
            means.Add(meanX);
            means.Add(meanY);


            //Now go through and check for large variation between adjacent contours: 
            //Currently using a difference of 2cm means between adjacent contours to flag an island
            for (int i = 0; i < numContours - 2; i++)
            {
                //Another for loop to check both x and y columns:
                for (int col = 0; col < 2; col++)
                {
                    double mean1 = np.mean(contours[i][Slice.All, col]);
                    double mean2 = np.mean(contours[i + 1][Slice.All, col]);
                    if (Math.Abs(mean1 - mean2) > maxSep)
                    {
                        numIslands++;
                        //Check which one is furthest from the ROI mean.
                        double dif1 = mean1 - means[col];
                        double dif2 = mean2 - means[col];

                        //remove the one furthest from the mean.
                        if (dif1 > dif2)    //remove contours[i]
                        {
                            contours.RemoveAt(i);
                        }
                        else     //Remove contours[i+1]
                        {
                            contours.RemoveAt(i + 1);
                        }
                    }
                }
            }
            if (numIslands == 0)
            {
                Console.WriteLine("No islands found");
            } else if (numIslands == 1)
            {
                Console.WriteLine(numIslands + " island detected and removed");
            }
            else
            {
                Console.WriteLine(numIslands + " islands detected and removed");
            }
            return contours;
        }

        public static List<NumSharp.NDArray> ClosedLooper(List<NumSharp.NDArray> contours)
        //Here we ensure that each contour forms a closed loop. 
        {

            for (int i = 0; i < contours.Count; i++)
            {
                int numRows = contours[i].size / 3 - 1;
                double x1 = contours[i][0, 0];
                double x2 = contours[i][numRows - 1, 0];
                double y1 = contours[i][0, 1];
                double y2 = contours[i][numRows - 1, 1];
                if ((x1 != x2)||(y1 != y2))
                {
                    contours[i] = np.vstack(contours[i], contours[i][0, Slice.All]);

                } 
            }
            return contours;
        }
        public static List<double[,]> ClosedLooper(List<double[,]> contours)
        //Here we ensure that each contour forms a closed loop. 
        {

            for (int i = 0; i < contours.Count; i++) {
                int numRows = contours[i].Length / 3;
                double x1 = contours[i][0, 0];
                double x2 = contours[i][numRows - 1, 0];
                double y1 = contours[i][0, 1];
                double y2 = contours[i][numRows - 1, 1];
                if ((x1 != x2) || (y1 != y2))
                {
                    contours[i] = FirstToLast(contours[i]);
                }
            }
            return contours;
        }
        public static double[,] ClosedLooper(double[,] contours)
        //Here we ensure that each contour forms a closed loop. 
        {
                int numRows = contours.Length / 3;
                double x1 = contours[0, 0];
                double x2 = contours[numRows - 1, 0];
                double y1 = contours[0, 1];
                double y2 = contours[numRows - 1, 1];
                if ((x1 != x2) || (y1 != y2))
            {
                    contours = FirstToLast(contours);
                }

            
            return contours;
        }
        public static List<double[]> ClosedLooper(List<double[]> contours)
        //Here we ensure that each contour forms a closed loop. 
        {
            double x1 = contours[0][0];
            double x2 = contours[contours.Count - 1][0];
            double y1 = contours[0][1];
            double y2 = contours[contours.Count - 1][1];
            if ((x1 != x2) || (y1 != y2))
            {
                contours = FirstToLast(contours);
            }         
            return contours;
        }

        public static double[,] FirstToLast(double[,] a)
            //Add the first row to the end of an array. (close loops)
        {
            double[,] b = new double[a.Length / 3 + 1, 3];
            int row = 0;
            for (int j = 0; j < a.Length; j++)
            {
                int column = j % 3;

                b[row, column] = a[row,column];

                if (column == 2)
                {
                    row++;
                }
            }
            b[a.Length / 3, 0] = a[0, 0];
            b[a.Length / 3, 1] = a[0, 1];
            b[a.Length / 3, 2] = a[0, 2];
            return b;
        }
        public static List<double[]> FirstToLast(List<double[]> a)
        //Add the first row to the end of an array. (close loops)
        {
            a.Add(a[0]);
            return a;
        }
        public static double[,] AddPoint(double[,] a, int index, double[] point)
        //Add the first row to the end of an array. (close loops)
        {
            double[,] b = new double[a.Length / 3 + 1, 3];
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
            b[index, 0] = point[0];
            b[index, 1] = point[1];
            b[index, 2] = point[2];
            if (index != a.Length / 3)
            {
                row = index + 1;
                for (int j = index * 3; j < b.Length-3; j++)
                {
                    int column = j % 3;

                    b[row, column] = a[row-1, column];

                    if (column == 2)
                    {
                        row++;
                    }
                }
            }
                        
            return b;
        }
    }

    


}




