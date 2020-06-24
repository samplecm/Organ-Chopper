using System;
using System.Collections.Generic;
using System.Linq;

using DicomChopper.Geom;

namespace DicomChopper
{
    public class Testing
    {
        public static void RunTests(List<List<double[,]>> contours, string organName)
        {
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("Running Tests");
            bool correctOrder = Testing.RegionOrderingTest18(contours, organName);
            if (!correctOrder)
            {
                Console.WriteLine("RegionOrderingTest18 Failed");
            }
        }
        public static bool RegionOrderingTest18(List<List<double[,]>> contours, string organName)
        //Test to ensure that the regions are ordered in the way they are intended to be (increasing z, increasing y, increasing z)
        {

            //First make a list of strings giving the intended ordering.
            List<string> correctOrder = new List<string>();
            correctOrder.Add("caudal - anterior - medial");
            correctOrder.Add("middle - anterior - medial");
            correctOrder.Add("superior - anterior - medial");
            correctOrder.Add("caudal - anterior - middle");
            correctOrder.Add("middle - anterior - middle");
            correctOrder.Add("superior - anterior - middle");
            correctOrder.Add("caudal - anterior - lateral");
            correctOrder.Add("middle - anterior - lateral");
            correctOrder.Add("superior - anterior - lateral");

            correctOrder.Add("caudal - posterior - medial");
            correctOrder.Add("middle - posterior - medial");
            correctOrder.Add("superior - posterior - medial");
            correctOrder.Add("caudal - posterior - middle");
            correctOrder.Add("middle - posterior - middle");
            correctOrder.Add("superior - posterior - middle");
            correctOrder.Add("caudal - posterior - lateral");
            correctOrder.Add("middle - posterior - lateral");
            correctOrder.Add("superior - posterior - lateral");
            List<string> actualOrder = new List<string>();
            //get the correct size, initialize so we can add at certain indices
            for (int i = 0; i < 18; i++)
            {
                actualOrder.Add("");
            }
            //find out if left or right organ:
            string organSide;
            if (organName.ToLower().Contains("l"))
            {
                organSide = "l";
            }else
            {
                organSide = "r";
            }

            List<List<double[,]>> temp = new List<List<double[,]>>();
            List<List<double[,]>> tempY = new List<List<double[,]>>();
            List<List<double[,]>> tempY2 = new List<List<double[,]>>();
            List<List<double[,]>> tempX = new List<List<double[,]>>();
            List<int> tempIndices = new List<int>();
            List<int> tempYIndices = new List<int>();
            List<int> tempY2Indices = new List<int>();
            List<int> tempXIndices = new List<int>();
            List<double> zVals = new List<double>();
            List<double> yVals = new List<double>();
            List<double> xVals = new List<double>();
            List<double> xVals2 = new List<double>();
            //Get the (numCutsX +1 ) x (numCuts Y + 1) smallest Z contours sequentially
            for (int i = 0; i < 18; i++)
            {
                //put the smallest z value for each region into zVals
                zVals.Add(contours[i][0][0, 2]);
            }
            zVals.Sort();
            for (int i = 0; i < 18; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    if (contours[i][0][0,2] == zVals[j])
                    {
                        temp.Add(contours[i]);
                        tempIndices.Add(i);
                        break;
                    }
                }
            }
            for (int i = 0; i < 6; i++)
            {
                yVals.Add(Stats.SliceMean(1, temp[i])); //add average y value for each region
            }
            yVals.Sort();
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (Stats.SliceMean(1, temp[i]) == yVals[j])
                    {
                        tempY.Add(temp[i]);
                        tempYIndices.Add(tempIndices[i]);
                        break;
                    }
                }
                for (int j = 3; j < 6; j++)
                {
                    if (Stats.SliceMean(1, temp[i]) == yVals[j])
                    {
                        tempY2.Add(temp[i]);
                        tempY2Indices.Add(tempIndices[i]);
                        break;
                    }
                }
            }

            for (int i = 0; i < 3; i++)
            {
                xVals.Add(Stats.SliceMean(0, tempY[i])); //add average y value for each region
            }
            for (int i = 0; i < 3; i++)
            {
                xVals2.Add(Stats.SliceMean(0, tempY2[i])); //add average y value for each region
            }
            xVals.Sort();
            xVals2.Sort();
            if (organSide == "l")
            {
                for (int i = 0; i < 3; i++)
                {
                    if (Stats.SliceMean(0, tempY[i]) == xVals[0])
                    {
                        actualOrder[tempYIndices[i]] = "caudal - anterior - medial";
                        
                    }
                    if (Stats.SliceMean(0, tempY[i]) == xVals[1])
                    {
                        actualOrder[tempYIndices[i]] = "caudal - anterior - middle";
                        
                    }
                    if (Stats.SliceMean(0, tempY[i]) == xVals[2])
                    {
                        actualOrder[tempYIndices[i]] = "caudal - anterior - lateral";
                        
                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[0])
                    {
                        actualOrder[tempY2Indices[i]] = "caudal - posterior - medial";
                        
                    }
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[1])
                    {
                        actualOrder[tempY2Indices[i]] = "caudal - posterior - middle";
                        
                    }
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[2])
                    {
                        actualOrder[tempY2Indices[i]] = "caudal - posterior - lateral";
                       
                    }
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    if (Stats.SliceMean(0, tempY[i]) == xVals[0])
                    {
                        actualOrder[tempYIndices[i]] = "caudal - anterior - lateral";
                        
                    }
                    if (Stats.SliceMean(0, tempY[i]) == xVals[1])
                    {
                        actualOrder[tempYIndices[i]] = "caudal - anterior - middle";
                        
                    }
                    if (Stats.SliceMean(0, tempY[i]) == xVals[2])
                    {
                        actualOrder[tempYIndices[i]] = "caudal - anterior - medial";
                        
                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[0])
                    {
                        actualOrder[tempY2Indices[i]] = "caudal - posterior - lateral";
                        
                    }
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[1])
                    {
                        actualOrder[tempY2Indices[i]] = "caudal - posterior - middle";
                        
                    }
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[2])
                    {
                        actualOrder[tempY2Indices[i]] = "caudal - posterior - medial";
                       
                    }
                }
            }
            temp.Clear();
            tempIndices.Clear();
            tempY.Clear();
            tempYIndices.Clear();
            tempY2.Clear();
            tempY2Indices.Clear();
            xVals.Clear();
            xVals2.Clear();
            yVals.Clear();
            for (int i = 0; i < 18; i++)
            {
                for (int j = 6; j < 12; j++)
                {
                    if (contours[i][0][0, 2] == zVals[j])
                    {
                        temp.Add(contours[i]);
                        tempIndices.Add(i);
                        break;
                    }
                }
            }
            for (int i = 0; i < 6; i++)
            {
                yVals.Add(Stats.SliceMean(1, temp[i])); //add average y value for each region
            }
            yVals.Sort();
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (Stats.SliceMean(1, temp[i]) == yVals[j])
                    {
                        tempY.Add(temp[i]);
                        tempYIndices.Add(tempIndices[i]);
                        break;
                    }
                }
                for (int j = 3; j < 6; j++)
                {
                    if (Stats.SliceMean(1, temp[i]) == yVals[j])
                    {
                        tempY2.Add(temp[i]);
                        tempY2Indices.Add(tempIndices[i]);
                        break;
                    }
                }
            }

            for (int i = 0; i < 3; i++)
            {
                xVals.Add(Stats.SliceMean(0, tempY[i])); //add average y value for each region
            }
            for (int i = 0; i < 3; i++)
            {
                xVals2.Add(Stats.SliceMean(0, tempY2[i])); //add average y value for each region
            }
            xVals.Sort();
            xVals2.Sort();
            if (organSide == "l")
            {
                for (int i = 0; i < 3; i++)
                {
                    if (Stats.SliceMean(0, tempY[i]) == xVals[0])
                    {
                        actualOrder[tempYIndices[i]] = "middle - anterior - medial";

                    }
                    if (Stats.SliceMean(0, tempY[i]) == xVals[1])
                    {
                        actualOrder[tempYIndices[i]] = "middle - anterior - middle";

                    }
                    if (Stats.SliceMean(0, tempY[i]) == xVals[2])
                    {
                        actualOrder[tempYIndices[i]] = "middle - anterior - lateral";

                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[0])
                    {
                        actualOrder[tempY2Indices[i]] = "middle - posterior - medial";

                    }
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[1])
                    {
                        actualOrder[tempY2Indices[i]] = "middle - posterior - middle";

                    }
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[2])
                    {
                        actualOrder[tempY2Indices[i]] = "middle - posterior - lateral";

                    }
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    if (Stats.SliceMean(0, tempY[i]) == xVals[0])
                    {
                        actualOrder[tempYIndices[i]] = "middle - anterior - lateral";

                    }
                    if (Stats.SliceMean(0, tempY[i]) == xVals[1])
                    {
                        actualOrder[tempYIndices[i]] = "middle - anterior - middle";

                    }
                    if (Stats.SliceMean(0, tempY[i]) == xVals[2])
                    {
                        actualOrder[tempYIndices[i]] = "middle - anterior - medial";

                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[0])
                    {
                        actualOrder[tempY2Indices[i]] = "middle - posterior - lateral";

                    }
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[1])
                    {
                        actualOrder[tempY2Indices[i]] = "middle - posterior - middle";

                    }
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[2])
                    {
                        actualOrder[tempY2Indices[i]] = "middle - posterior - medial";

                    }
                }
            }
            temp.Clear();
            tempIndices.Clear();
            tempY.Clear();
            tempYIndices.Clear();
            tempY2.Clear();
            tempY2Indices.Clear();
            xVals.Clear();
            xVals2.Clear();
            yVals.Clear();
            for (int i = 0; i < 18; i++)
            {
                for (int j = 12; j < 18; j++)
                {
                    if (contours[i][0][0, 2] == zVals[j])
                    {
                        temp.Add(contours[i]);
                        tempIndices.Add(i);
                        break;
                    }
                }
            }
            for (int i = 0; i < 6; i++)
            {
                yVals.Add(Stats.SliceMean(1, temp[i])); //add average y value for each region
            }
            yVals.Sort();
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (Stats.SliceMean(1, temp[i]) == yVals[j])
                    {
                        tempY.Add(temp[i]);
                        tempYIndices.Add(tempIndices[i]);
                        break;
                    }
                }
                for (int j = 3; j < 6; j++)
                {
                    if (Stats.SliceMean(1, temp[i]) == yVals[j])
                    {
                        tempY2.Add(temp[i]);
                        tempY2Indices.Add(tempIndices[i]);
                        break;
                    }
                }
            }

            for (int i = 0; i < 3; i++)
            {
                xVals.Add(Stats.SliceMean(0, tempY[i])); //add average y value for each region
            }
            for (int i = 0; i < 3; i++)
            {
                xVals2.Add(Stats.SliceMean(0, tempY2[i])); //add average y value for each region
            }
            xVals.Sort();
            xVals2.Sort();
            if (organSide == "l")
            {
                for (int i = 0; i < 3; i++)
                {
                    if (Stats.SliceMean(0, tempY[i]) == xVals[0])
                    {
                        actualOrder[tempYIndices[i]] = "superior - anterior - medial";

                    }
                    if (Stats.SliceMean(0, tempY[i]) == xVals[1])
                    {
                        actualOrder[tempYIndices[i]] = "superior - anterior - middle";

                    }
                    if (Stats.SliceMean(0, tempY[i]) == xVals[2])
                    {
                        actualOrder[tempYIndices[i]] = "superior - anterior - lateral";

                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[0])
                    {
                        actualOrder[tempY2Indices[i]] = "superior - posterior - medial";

                    }
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[1])
                    {
                        actualOrder[tempY2Indices[i]] = "superior - posterior - middle";

                    }
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[2])
                    {
                        actualOrder[tempY2Indices[i]] = "superior - posterior - lateral";

                    }
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    if (Stats.SliceMean(0, tempY[i]) == xVals[0])
                    {
                        actualOrder[tempYIndices[i]] = "superior - anterior - lateral";

                    }
                    if (Stats.SliceMean(0, tempY[i]) == xVals[1])
                    {
                        actualOrder[tempYIndices[i]] = "superior - anterior - middle";

                    }
                    if (Stats.SliceMean(0, tempY[i]) == xVals[2])
                    {
                        actualOrder[tempYIndices[i]] = "superior - anterior - medial";

                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[0])
                    {
                        actualOrder[tempY2Indices[i]] = "superior - posterior - lateral";

                    }
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[1])
                    {
                        actualOrder[tempY2Indices[i]] = "superior - posterior - middle";

                    }
                    if (Stats.SliceMean(0, tempY2[i]) == xVals2[2])
                    {
                        actualOrder[tempY2Indices[i]] = "superior - posterior - medial";

                    }
                }
            }

            if (actualOrder.SequenceEqual(correctOrder))
            {
                return true;
            }else
            {
                return false;
            }

        }


            
        }
    }

