using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DicomChopper.Geom;
using DicomChopper.Segmentation;
using ILNumerics.Drawing;

namespace DicomChopper.Segmentation
{
    public class Chopper
    {
        public static List<List<double[,]>> Chop(List<double[,]> contoursTemp, int numCutsX, int numCutsY, int numCutsZ)
        {

            //Now make the axial cuts first: 
            double[] zCuts = new double[numCutsZ];
            zCuts = BestCutZ(contoursTemp, numCutsZ);    //find out where to cut the structure along z
            List<List<double[,]>> axialDivs;
            if (numCutsZ != 0)
            {
                axialDivs = ZChop(contoursTemp, zCuts); //Perform the z cuts //holds the different sections after just the z-cuts.
            }else
            {
                axialDivs = new List<List<double[,]>>();
                axialDivs.Add(contoursTemp);
            }
            //For each z chunk, need to recursively chop into y bits, and each y bit into x bits. 

                List<List<double[,]>> contoursY = new List<List<double[,]>>(); //to hold all chopped contours after y cuts
            if (numCutsY != 0)
            {
                for (int i = 0; i < axialDivs.Count; i++)
                {
                    List<List<double[,]>> temp = YChop(axialDivs[i], numCutsY);
                    for (int j = 0; j < temp.Count; j++)
                    {
                        contoursY.Add(temp[j].GetRange(0, temp[j].Count));
                    }
                }
            }else
            {
                contoursY = axialDivs;
            }

            //Now do the x chops to finish up!

                List<List<double[,]>> contours = new List<List<double[,]>>(); //to hold all chopped contours after all cuts
            if (numCutsX != 0)
            {
                for (int i = 0; i < contoursY.Count; i++)
                {
                    List<List<double[,]>> temp = XChop(contoursY[i], numCutsX);
                    for (int j = 0; j < temp.Count; j++)
                    {
                        contours.Add(temp[j].GetRange(0, temp[j].Count));
                    }
                }

            }else
            {
                contours = contoursY;
            }
            return contours;



        }

        public static List<List<double[,]>> XChop(List<double[,]> contours, int numCutsX)
        {
            double[] xCuts = BestCutX(contours, numCutsX, 0.0001);
            // add intersection points
            for (int i = 0; i < contours.Count; i++)
            {
                contours[i] = AddIntersectionsX(contours[i], xCuts);
                contours[i] = ContourFixing.ClosedLooper(contours[i]);
            }

            //////////////////////////////////////
            //Now divide into separate parts.
            ///////////////////////////////////////
            List<List<double[,]>> finalContours = new List<List<double[,]>>();
            //make a list for each x division for the current contour.
            List<List<double[]>> divisions = new List<List<double[]>>();

            //Make the list the correct size so that there is an item for each x division.
            for (int div = 0; div <= xCuts.Length; div++)
            {
                divisions.Add(new List<double[]>());
                finalContours.Add(new List<double[,]>());
            }
            for (int i = 0; i < contours.Count; i++)    //for all of the contours
            {
                divisions.Clear();
                for (int div = 0; div <= xCuts.Length; div++)
                {
                    divisions.Add(new List<double[]>());
                }
                for (int x = 0; x <= xCuts.Length; x++) //a section for every cut, + 1
                {
                    for (int j = 0; j < contours[i].Length / 3; j++)    //loop through all points
                    {
                        if (x == 0)
                        {
                            if (contours[i][j, 0] <= xCuts[x])
                            {
                                divisions[x].Add(new double[] { contours[i][j, 0], contours[i][j, 1], contours[i][j, 2] });
                            }
                        }
                        else
                        {
                            if (contours[i][j, 0] >= xCuts[x - 1])
                            {
                                divisions[x].Add(new double[] { contours[i][j, 0], contours[i][j, 1], contours[i][j, 2] });
                            }
                        }
                    }
                }
                //at this point divisions has a list item holding a list of array points for each cut.
                //Need to now make double arrays for each of these and add them to new final list.
                double[,] temp;
                for (int x = 0; x <= xCuts.Length; x++) //a section for every cut, + 1
                {
                    temp = new double[divisions[x].Count, 3];
                    for (int row = 0; row < temp.Length / 3; row++)
                    {
                        temp[row, 0] = divisions[x][row][0];
                        temp[row, 1] = divisions[x][row][1];
                        temp[row, 2] = divisions[x][row][2];
                    }
                    if (temp.Length != 0)
                    {
                        temp = ContourFixing.ClosedLooper(temp);
                        finalContours[x].Add(temp);
                    }
                }
            }
            return finalContours;
        }
        public static double[] BestCutX(List<double[,]> contours, int numCuts, double errorTolerance)
        {
            double[] xCuts = new double[numCuts];
            double area = 0;
            double maxX = -1000;
            double minX = 1000;
            double error, xCut, newArea;

            //Get total area of contours:
            for (int i = 0; i < contours.Count; i++)
            {
                if (contours[i].Length != 0)
                {
                    area += Geom.Geometry.Area(contours[i]);
                }
            }
            for (int cut = 0; cut < numCuts; cut++)
            {
                double areaGoal = (double)(cut + 1) / (numCuts + 1); // fractional area goal for each cut.
                //Now get the max and min Y for the structure
                for (int j = 0; j < contours.Count; j++)
                {
                    if (contours[j].Length != 0)
                    {
                        for (int row = 0; row < contours[j].Length / 3; row++)
                        //get the maximum, minimum y
                        {
                            if (contours[j][row, 0] > maxX)
                            {
                                maxX = contours[j][row, 0];
                            }
                            if (contours[j][row, 0] < minX)
                            {
                                minX = contours[j][row, 0];
                            }

                        }
                    }
                }
                //Now iteratively get equal volumes to the error allowance set
                List<double[,]> tempContours = new List<double[,]>();
                List<double[]> cutContours = new List<double[]>(); //store contour as a list to append easily.
                do
                {

                    tempContours.Clear();
                    xCut = (minX + maxX) / 2;
                    newArea = 0;
                    //First add the intersection points: 
                    for (int i = 0; i < contours.Count; i++)
                    {
                        cutContours.Clear();
                        if (contours[i].Length != 0)
                        {
                            tempContours.Add(AddIntersectionsX(contours[i], xCut));
                            tempContours[i] = ContourFixing.ClosedLooper(tempContours[i]);
                            //now make a new contour with points below yCut.
                            for (int j = 0; j < tempContours[i].Length / 3; j++)
                            {
                                if (tempContours[i][j, 0] <= xCut)
                                {
                                    cutContours.Add(new double[] { tempContours[i][j, 0], tempContours[i][j, 1], tempContours[i][j, 2] });
                                }
                            }
                            if (cutContours.Count != 0)
                            {
                                cutContours = ContourFixing.ClosedLooper(cutContours);
                                newArea += Geom.Geometry.Area(cutContours);
                            }
                        }
                    }
                    //Now compare areas:
                    if (newArea / area < areaGoal)
                    {
                        minX = xCut;
                    }
                    else if (newArea / area > areaGoal)
                    {
                        maxX = xCut;
                    }
                    error = Math.Abs((newArea / area) - areaGoal);

                } while (error > errorTolerance);

                xCuts[cut] = xCut;
            }
            return xCuts;
        }

        public static double[,] AddIntersectionsX(double[,] contours, double xCut)
        {
            double m;
            double yNew;
            double[,] finalContours = contours;

            int numAdded = 1; //start at one, increment after adding each point, to keep track of where to add next additional point (add to index)
            //index 0 outside of loop:
            if ((contours[0, 0] > xCut) & (contours[contours.Length / 3 - 1, 0] < xCut))
            {
                m = (contours[0, 0] - contours[contours.Length / 3 - 1, 0]) / (contours[0, 1] - contours[contours.Length / 3 - 1, 1]);
                yNew = ((xCut - contours[contours.Length / 3 - 1, 0]) / m) + contours[contours.Length / 3 - 1, 1];
                finalContours = ContourFixing.AddPoint(finalContours, 0, new double[] { xCut, yNew, contours[0, 2] });
                numAdded++;
            }
            if ((contours[0, 0] < xCut) & (contours[contours.Length / 3 - 1, 0] > xCut))
            {
                m = (contours[contours.Length / 3 - 1, 0] - contours[0, 0]) / (contours[contours.Length / 3 - 1, 1] - contours[0, 1]);
                yNew = ((xCut - contours[0, 0]) / m) + contours[0, 1];
                finalContours = ContourFixing.AddPoint(finalContours, contours.Length / 3, new double[] { xCut, yNew, contours[0, 2] });
            }

            for (int i = 0; i < contours.Length / 3 - 1; i++)    //for all points, except last one will be out of loop
            {
                if ((contours[i, 0] < xCut) & (contours[i + 1, 0] > xCut)) //if x is below the cut
                {
                    m = (contours[i + 1, 0] - contours[i, 0]) / (contours[i + 1, 1] - contours[i, 1]);
                    yNew = ((xCut - contours[i, 0]) / m) + contours[i, 1];
                    finalContours = ContourFixing.AddPoint(finalContours, i + numAdded, new double[] { xCut, yNew, contours[0, 2] });
                    numAdded++;

                }
                else if ((contours[i, 0] > xCut) & (contours[i + 1, 0] < xCut))
                {
                    m = (contours[i + 1, 0] - contours[i, 0]) / (contours[i + 1, 1] - contours[i, 1]);
                    yNew = ((xCut - contours[i, 0]) / m) + contours[i, 1];
                    finalContours = ContourFixing.AddPoint(finalContours, i + numAdded, new double[] { xCut, yNew, contours[0, 2] });
                    numAdded++;
                }
            }
            return finalContours;

        }
        public static double[,] AddIntersectionsX(double[,] structures, double[] xCut)
        {
            double m;
            double yNew;
            double[,] finalContours = structures;
            for (int x = 0; x < xCut.Length; x++)
            {
                double[,] contours = finalContours;
                int numConts = finalContours.Length / 3;
                int numAdded = 1; //start at one, increment after adding each point, to keep track of where to add next additional point (add to index)
                                  //index 0 outside of loop:
                if ((contours[0, 0] > xCut[x]) & (contours[contours.Length / 3 - 1, 0] < xCut[x]))
                {
                    m = (contours[0, 0] - contours[contours.Length / 3 - 1, 0]) / (contours[0, 1] - contours[contours.Length / 3 - 1, 1]);
                    yNew = ((xCut[x] - contours[contours.Length / 3 - 1, 0]) / m) + contours[contours.Length / 3 - 1, 1];
                    finalContours = ContourFixing.AddPoint(finalContours, 0, new double[] { xCut[x], yNew, contours[0, 2] });
                    numAdded++;
                }
                if ((contours[0, 0] < xCut[x]) & (contours[contours.Length / 3 - 1, 0] > xCut[x]))
                {
                    m = (contours[contours.Length / 3 - 1, 0] - contours[0, 0]) / (contours[contours.Length / 3 - 1, 1] - contours[0, 1]);
                    yNew = ((xCut[x] - contours[0, 0]) / m) + contours[0, 1];
                    finalContours = ContourFixing.AddPoint(finalContours, contours.Length / 3, new double[] { xCut[x], yNew, contours[0, 2] });
                }

                for (int i = 0; i < contours.Length / 3 - 1; i++)    //for all points, except last one will be out of loop
                {
                    if ((contours[i, 0] < xCut[x]) & (contours[i + 1, 0] > xCut[x])) //if y is below the cut
                    {
                        m = (contours[i + 1, 0] - contours[i, 0]) / (contours[i + 1, 1] - contours[i, 1]);
                        yNew = ((xCut[x] - contours[i, 0]) / m) + contours[i, 1];
                        finalContours = ContourFixing.AddPoint(finalContours, i + numAdded, new double[] { xCut[x], yNew, contours[0, 2] });
                        numAdded++;

                    }
                    else if ((contours[i, 0] > xCut[x]) & (contours[i + 1, 0] < xCut[x]))
                    {
                        m = (contours[i + 1, 0] - contours[i, 0]) / (contours[i + 1, 1] - contours[i, 1]);
                        yNew = ((xCut[x] - contours[i, 0]) / m) + contours[i, 1];
                        finalContours = ContourFixing.AddPoint(finalContours, i + numAdded, new double[] { xCut[x], yNew, contours[0, 2] });
                        numAdded++;
                    }
                }
            }
            return finalContours;

        }

        public static List<List<double[,]>> YChop(List<double[,]> contours, int numCutsY)
            {
            
                double[] yCuts = BestCutY(contours, numCutsY, 0.0001);    //Contains y-values for cut locations

                // add intersection points
                for (int i = 0; i < contours.Count; i++)
                {
                    contours[i] = AddIntersectionsY(contours[i], yCuts);
                    contours[i] = ContourFixing.ClosedLooper(contours[i]); 
                }
                
                //////////////////////////////////////
                //Now divide into separate parts.
                ///////////////////////////////////////
                List<List<double[,]>> finalContours = new List<List<double[,]>>();
                //make a list for each y division for the current contour.
                List<List<double[]>> divisions = new List<List<double[]>>();
            
                //Make the list the correct size so that there is an item for each y division.
                for (int div = 0; div <= yCuts.Length; div++)
                {
                    divisions.Add(new List<double[]>());
                    finalContours.Add(new List<double[,]>());
                }


                for (int i = 0; i < contours.Count; i++)    //for all of the contours
                {
                    divisions.Clear();
                    for (int div = 0; div <= yCuts.Length; div++)
                    {
                        divisions.Add(new List<double[]>());                 
                    }
                    for (int y = 0; y <= yCuts.Length; y++) //a section for every cut, + 1
                    {
                        for (int j = 0; j < contours[i].Length / 3; j++)    //loop through all points
                        {
                            if (y == 0)
                            {
                                if (contours[i][j,1] <= yCuts[y])
                                {
                                    divisions[y].Add(new double[] { contours[i][j, 0], contours[i][j, 1], contours[i][j, 2] });
    
                                }
                            }
                        else
                            {
                                if (contours[i][j,1] >= yCuts[y - 1])
                                {
                                    divisions[y].Add(new double[] { contours[i][j, 0], contours[i][j, 1], contours[i][j, 2] });
                                }
                            }
                        }
                    }
                    //at this point divisions has a list item holding a list of array points for each cut.
                    //Need to now make double arrays for each of these and add them to new final list.
                    double[,] temp;
                    for (int y = 0; y <= yCuts.Length; y++) //a section for every cut, + 1
                    {
                        temp = new double[divisions[y].Count, 3];
                        for (int row = 0; row < temp.Length / 3; row++)
                        {
                            temp[row, 0] = divisions[y][row][0];
                            temp[row, 1] =  divisions[y][row][1];
                            temp[row, 2] = divisions[y][row][2];
                        }
                    
                        if (temp.Length != 0)
                        {
                            temp = ContourFixing.ClosedLooper(temp);
                            finalContours[y].Add(temp);
                        }

                    }
                }

            return finalContours;
        }
        public static double[] BestCutY(List<double[,]> contours, int numCuts, double errorTolerance)
        {
            double[] yCuts = new double[numCuts];
            double area = 0;
            double maxY = -1000;
            double minY = 1000;
            double error, yCut, newArea;

            //Get the total area of contours: 
            for (int i = 0; i < contours.Count; i++)
            {
                if (contours[i].Length != 0)
                {
                    area += Geom.Geometry.Area(contours[i]);
                }

            }
            for (int cut = 0; cut < numCuts; cut++)
            {
                double areaGoal = (double)(cut+1) / (numCuts + 1); // fractional area goal for each cut.
                //Now get the max and min Y for the structure
                for (int j = 0; j < contours.Count; j++)
                {
                    if (contours[j].Length != 0)
                    {
                        for (int row = 0; row < contours[j].Length / 3; row++)
                            //get the maximum, minimum y
                        {
                            if (contours[j][row,1] > maxY)
                            {
                                maxY = contours[j][row, 1];
                            }
                            if (contours[j][row, 1] < minY)
                            {
                                minY = contours[j][row, 1];
                            }

                        }
                    }
                }
                //Now iteratively get equal volumes to the error allowance set
                List<double[,]> tempContours = new List<double[,]>();
                List<double[]> cutContours = new List<double[]>(); //store contour as a list to append easily.
                do
                {
                    
                    tempContours.Clear();
                    yCut = (minY + maxY) / 2;
                    newArea = 0;
                    //First add the intersection points: 
                    for (int i = 0; i < contours.Count; i++)
                    {
                        cutContours.Clear();
                        if (contours[i].Length != 0)
                        {
                            tempContours.Add(AddIntersectionsY(contours[i], yCut));
                            tempContours[i] = ContourFixing.ClosedLooper(tempContours[i]);
                            //now make a new contour with points below yCut.
                            for (int j = 0; j < tempContours[i].Length / 3; j++)
                            {
                                if (tempContours[i][j, 1] <= yCut)
                                {
                                    cutContours.Add(new double[] { tempContours[i][j, 0], tempContours[i][j, 1], tempContours[i][j, 2] });
                                }
                            }
                            if (cutContours.Count != 0)
                            {
                                cutContours = ContourFixing.ClosedLooper(cutContours);
                                newArea += Geom.Geometry.Area(cutContours);
                            }
                        }
                    }
                    //Now compare areas:
                    if (newArea/area < areaGoal)
                    {
                        minY = yCut;
                    }else if (newArea/area > areaGoal)
                    {
                        maxY = yCut;
                    }
                    error = Math.Abs((newArea / area) - areaGoal);

                } while (error > errorTolerance);

                yCuts[cut] = yCut;
            }
            return yCuts;

        }
        public static double[,] AddIntersectionsY(double[,] contours, double yCut)
        {
            double m;
            double xNew;
            double[,] finalContours = contours;

            int numAdded = 1; //start at one, increment after adding each point, to keep track of where to add next additional point (add to index)
            //index 0 outside of loop:
            if ((contours[0,1] > yCut)&(contours[contours.Length / 3 - 1,1] < yCut)){
                m = (contours[0, 1] - contours[contours.Length / 3 - 1, 1]) / (contours[0, 0] - contours[contours.Length / 3 - 1, 0]);
                xNew = ((yCut - contours[contours.Length / 3 - 1, 1]) / m) + contours[contours.Length / 3 - 1, 0];
                finalContours = ContourFixing.AddPoint(finalContours, 0, new double[] { xNew, yCut, contours[0, 2] });
                numAdded++;
            }
            if ((contours[0, 1] < yCut) & (contours[contours.Length / 3 - 1, 1] > yCut))
            {
                m = (contours[contours.Length / 3 - 1, 1] - contours[0, 1]) / (contours[contours.Length / 3 - 1, 0] - contours[0, 0]);
                xNew = ((yCut - contours[0, 1]) / m) + contours[0, 0];
                finalContours = ContourFixing.AddPoint(finalContours, contours.Length/3, new double[] { xNew, yCut, contours[0, 2] });
            }

            for (int i = 1; i < contours.Length / 3 - 1; i++)    //for all points, except last one will be out of loop
            {
                if ((contours[i,1] < yCut)& (contours[i + 1, 1] > yCut)) //if y is below the cut
                {
                    m = (contours[i + 1, 1] - contours[i, 1]) / (contours[i + 1, 0] - contours[i, 0]);
                    xNew = ((yCut - contours[i, 1]) / m) + contours[i, 0];
                    finalContours = ContourFixing.AddPoint(finalContours, i+numAdded, new double[] {xNew,yCut,contours[0, 2]});
                    numAdded++;
                    
                }else if ((contours[i,1] > yCut)&(contours[i + 1, 1] < yCut))
                {
                    m = (contours[i + 1, 1] - contours[i, 1]) / (contours[i + 1, 0] - contours[i, 0]);
                    xNew = ((yCut - contours[i, 1]) / m) + contours[i, 0];
                    finalContours = ContourFixing.AddPoint(finalContours, i+numAdded, new double[] { xNew, yCut, contours[0, 2] });
                    numAdded++;
                }
            }
            return finalContours;

        }
        public static double[,] AddIntersectionsY(double[,] structure, double[] yCut)
        {
            double m;
            double xNew;
            double[,] finalContours = structure;
            for (int y = 0; y < yCut.Length; y++)
          
            {
                double[,] contours = finalContours;
                int numConts = finalContours.Length / 3;
                int numAdded = 1; //start at one, increment after adding each point, to keep track of where to add next additional point (add to index)
                                  //index 0 outside of loop:
                if ((contours[0, 1] > yCut[y]) & (contours[contours.Length / 3 - 1, 1] < yCut[y]))
                {
                    m = (contours[0, 1] - contours[contours.Length / 3 - 1, 1]) / (contours[0, 0] - contours[contours.Length / 3 - 1, 0]);
                    xNew = ((yCut[y] - contours[contours.Length / 3 - 1, 1]) / m) + contours[contours.Length / 3 - 1, 0];
                    finalContours = ContourFixing.AddPoint(finalContours, 0, new double[] { xNew, yCut[y], contours[0, 2] });
                    numAdded++;
                }
                if ((contours[0, 1] < yCut[y]) & (contours[contours.Length / 3 - 1, 1] > yCut[y]))
                {
                    m = (contours[contours.Length / 3 - 1, 1] - contours[0, 1]) / (contours[contours.Length / 3 - 1, 0] - contours[0, 0]);
                    xNew = ((yCut[y] - contours[0, 1]) / m) + contours[0, 0];
                    finalContours = ContourFixing.AddPoint(finalContours, contours.Length / 3, new double[] { xNew, yCut[y], contours[0, 2] });
                }
                for (int i = 1; i < numConts - 1; i++)    //for all points, except last one will be out of loop
                {
                    if ((contours[i, 1] < yCut[y]) & (contours[i + 1, 1] > yCut[y])) //if y is below the cut
                    {
                        m = (contours[i + 1, 1] - contours[i, 1]) / (contours[i + 1, 0] - contours[i, 0]);
                        xNew = ((yCut[y] - contours[i, 1]) / m) + contours[i, 0];
                        finalContours = ContourFixing.AddPoint(finalContours, i + numAdded, new double[] { xNew, yCut[y], contours[0, 2] });
                        numAdded++;
                    }
                    else if ((contours[i, 1] > yCut[y]) & (contours[i + 1, 1] < yCut[y]))
                    {
                        m = (contours[i + 1, 1] - contours[i, 1]) / (contours[i + 1, 0] - contours[i, 0]);
                        xNew = ((yCut[y] - contours[i, 1]) / m) + contours[i, 0];
                        finalContours = ContourFixing.AddPoint(finalContours, i + numAdded, new double[] { xNew, yCut[y], contours[0, 2] });
                        numAdded++;
                    }
                }
            }
                return finalContours;
            }
        public static double[] BestCutZ(List<double[,]> contours, int numCuts)
        {
            double[] zCuts = new double[numCuts];    //to hold z value of cut locations
            int numConts = contours.Count();
            double totalVolume = 0;
            double deltaZ = Math.Abs(contours[0][0, 2] - contours[1][0, 2]);    //distance between adjacent contours
            double[] contourAreas = new double[numConts];

            for (int i = 0; i < numConts; i++) //right now using area of every contour but last... should last be included? 
            {
                contourAreas[i] = Geom.Geometry.Area(contours[i]);
                if (i != numConts-1)
                {
                    totalVolume += contourAreas[i] * deltaZ;
                }        
            }

            //Now find the right cut spots:
            //
            int contIndex;
            double subVolume;
            for (int i = 1; i <= numCuts; i++)
            {
                double volumeGoal = (double)i / (numCuts + 1);
                contIndex = 0;
                subVolume = 0;//eg. if 2 cuts, volume goal before first cut = 1/3, goal before second = 2/3.

                while (subVolume < volumeGoal * totalVolume)
                {
                    subVolume += contourAreas[contIndex] * deltaZ;
                    contIndex++;
                }
                //The cut will be between indices contIndex-1 and contIndex.
                //Now determine the volume below and ontop of the cut.
                double volumeBelow = 0;
                double avgArea;
                for (int j = 0; j < contIndex - 1; j++)
                {
                    //first get average area between two contours, used to approximate volume between the two.
                    avgArea = 0.5 * (contourAreas[j] + contourAreas[j + 1]);
                    volumeBelow += avgArea * deltaZ;

                }

                //Now get the average area for the slicing region:
                avgArea = 0.5 * (contourAreas[contIndex-1]+contourAreas[contIndex]);


                zCuts[i-1] = contours[contIndex - 1][0, 2] + (volumeGoal*totalVolume-volumeBelow)/avgArea;
            }
            return zCuts;


        }

        public static List<List<double[,]>> ZChop(List<double[,]> contours, double[] zCuts)
        {
            int[] sliceIndices = new int[zCuts.Length];
            double[,] newContour;
            int[] contoursZ;
            int numPoints;
            for (int i = 0; i < zCuts.Length; i++)    //for each cut
            {
                //get the closest 2 contours for the cut
                contoursZ = ClosestContourZ(zCuts[i], contours);
                numPoints = contours[contoursZ[0]].Length / 3;
                newContour = new double[numPoints,3];

                for (int j = 0; j < numPoints; j++)
                {
                    double x = contours[contoursZ[0]][j, 0];
                    double y = contours[contoursZ[0]][j, 1];
                    double z = contours[contoursZ[0]][j, 2];
                    //Now get idx, the row index in the second closest contour. Then interpolate between the two.
                    int idx = ClosestPoint(x,y,contours[contoursZ[1]]);
                    double[] point1 = { x, y, z };
                    double[] point2 = { contours[contoursZ[1]][idx, 0], contours[contoursZ[1]][idx, 1], contours[contoursZ[1]][idx, 2] };
                    double[] newPoint = InterpolateXY(point1,point2, zCuts[i]);
                    //now add newPoint to the newContour:
                    newContour[j,0] = newPoint[0];
                    newContour[j, 1] = newPoint[1];
                    newContour[j, 2] = newPoint[2];
                }
                //Add this new contour to the list.
                sliceIndices[i] = Math.Max(contoursZ[0], contoursZ[1]);
                contours.Insert(sliceIndices[i],newContour);
                //at this point new axial division contours have been added to original set. 
            }

            List<List<double[,]>> finalContours = new List<List<double[,]>>();
            List<double[,]> tempList = new List<double[,]>();
            
            for (int cut = 0; cut <= zCuts.Length; cut++)
            {
                tempList.Clear();

                if (cut == 0)
                {
                    for (int i = 0; i <= sliceIndices[cut]; i++)
                    {
                        tempList.Add(contours[i]);
                    }
                }else if (cut > 0 & cut < zCuts.Length)
                {
                    for (int i = sliceIndices[cut-1]; i <= sliceIndices[cut]; i++)
                    {
                        tempList.Add(contours[i]);
                    }

                }else
                {
                    for (int i = sliceIndices[cut - 1]; i <= contours.Count-1; i++)
                    {
                        tempList.Add(contours[i]);
                    }
                }
                finalContours.Add(tempList.GetRange(0,tempList.Count));
                //finalContours[cut] = ContourFixing.ClosedLooper(finalContours[cut]);
            }
            //now form closed loops: 

            return finalContours;
        }
        public static int[] ClosestContourZ(double zCut, List<double[,]> contours)
        {
            double temp = 1000;
            int[] closestContours = new int[2];

            //first find the closest contour to the zCut: 
            for (int i = 0; i < contours.Count; i++)
            {
                double contourDistance = Math.Abs(contours[i][0, 2] - zCut);
                if (contourDistance < temp)
                {
                    closestContours[0] = i;
                    temp = contourDistance;
                }
            }
            //Now get the second closest contour: 
            temp = 1000;
            for (int i = 0; i < contours.Count; i++)
            {
                if (i == closestContours[0])
                {
                    continue;
                }
                double contourDistance = Math.Abs(contours[i][0, 2] - zCut);
                if (contourDistance < temp)
                {
                    closestContours[1] = i;
                    temp = contourDistance;
                }
            }
            return closestContours;
        }

        public static int ClosestPoint(double x, double y, double[,] points)
        {
            double m = 1000;
            int closestPoint = 1000;
            for (int i = 0; i < points.Length / 3; i++)
            {
                double diff = Math.Sqrt(Math.Pow((x - points[i,0]),2) + Math.Pow((y- points[i,1]),2));    //difference between points in xy plane
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

    }

}
