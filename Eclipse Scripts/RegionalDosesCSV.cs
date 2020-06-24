using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.IO;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;


namespace VMS.TPS
{

    public class Script
    {
        public void Execute(ScriptContext context)
        {
            int numCutsX = 2;
            int numCutsY = 1;
            int numCutsZ = 2;
            int SSFactor = 4; //supersampling factors
            int SSFactorZ = 1;
            // Check for patient loaded
            if (context.Patient == null)
            {
                MessageBox.Show("Shucks: No patient selected! :(");
                return;
            }
            // Check for patient plan loaded
            PlanSetup plan = context.PlanSetup;
            if (plan == null)
            {
                MessageBox.Show("Darn: No plan loaded! :( ");
                return;
            }
            if (context.StructureSet == null)
            {
                MessageBox.Show("Oh No!: no structure set found! :(");
            }


            Patient testPatient = context.Patient;
            PlanSetup plan1 = context.PlanSetup;
            Image image = context.Image;
            Dose dose = plan1.Dose;
            plan.DoseValuePresentation = DoseValuePresentation.Absolute;
            DoseValue.DoseUnit doseUnit = dose.DoseMax3D.Unit;
            DoseMatrix doses = GetDoseMatrix(dose, image, plan1);
            //Get the dose matrix dimensions:	

            //MessageBox.Show(doses.Matrix[39, 39, 19].ToString());
            //MessageBox.Show(dose.DoseMax3D.Unit.ToString());	


            //Now get the contours. Get the contralateral parotid as parotid with the smallest dose.
            StructureSet structureSet = context.StructureSet;
            var tuple = GetContours(structureSet, plan1, image);    //returns contours and organ name.
            List<double[,]> contours = tuple.Item1;
            string organName = tuple.Item2;
            List<Structure> ROI = tuple.Item3;
            DoseValue wholeMean = CalculateMeanDose(plan1, ROI[0]);
            //MessageBox.Show(contours[0][0, 2].ToString());
            List<List<double[,]>> choppedContours = Chop(contours, numCutsX, numCutsY, numCutsZ, organName);
            double[,] meanDoses = MeanDoses(choppedContours, doses, SSFactor, SSFactorZ, organName);
            //make CSV for meandoses
            string fileName = plan1.Id + ".csv";
            string path = Path.Combine(path, testPatient.LastName);
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(path, fileName)))
            {
                outputFile.WriteLine("Regional mean doses (Gy):");
                outputFile.WriteLine("SubRegion, Dose:");

                for (int i = 0; i < meanDoses.GetLength(0) - 1; i++)
                {
                    outputFile.WriteLine((i + 1) + ", " + String.Format("{0:0.00}", meanDoses[i, 0]));
                }

                outputFile.WriteLine("Whole Mean Dose, " + String.Format("{0:0.00}", meanDoses[meanDoses.GetLength(0) - 1, 0]));
                outputFile.WriteLine("Whole mean dose error, " + wholeMean.Dose.ToString());
                
                bool correctOrder = RegionOrderingTest18(choppedContours, organName);
                if (correctOrder)
                {
                    outputFile.WriteLine("Passed");
                }
                else
                {
                    outputFile.WriteLine("Failed");
                }            
            }



        }

        public static DoseValue CalculateMeanDose(PlanSetup plan, Structure structure)
        {
            Dose dose = plan.Dose;
            if (dose == null)
                return new DoseValue(Double.NaN, DoseValue.DoseUnit.Unknown);

            plan.DoseValuePresentation = DoseValuePresentation.Absolute;

            double sum = 0.0;
            int count = 0;

            double xres = 2.5;
            double yres = 2.5;
            double zres = 2.5;

            int xcount = (int)((dose.XRes * dose.XSize) / xres);
            System.Collections.BitArray segmentStride = new System.Collections.BitArray(xcount);
            double[] doseArray = new double[xcount];
            DoseValue.DoseUnit doseUnit = dose.DoseMax3D.Unit;

            for (double z = 0; z < dose.ZSize * dose.ZRes; z += zres)
            {
                for (double y = 0; y < dose.YSize * dose.YRes; y += yres)
                {
                    // sum of dose values inside of structure to the power of 'a' etc. if a = 1 then its the mean
                    VVector start = dose.Origin +
                                    dose.YDirection * y +
                                    dose.ZDirection * z;
                    VVector end = start + dose.XDirection * dose.XRes * dose.XSize;

                    SegmentProfile segmentProfile = structure.GetSegmentProfile(start, end, segmentStride);
                    DoseProfile doseProfile = null;

                    for (int i = 0; i < segmentProfile.Count; i++)
                    {
                        if (segmentStride[i])
                        {
                            if (doseProfile == null)
                            {
                                doseProfile = dose.GetDoseProfile(start, end, doseArray);
                            }

                            double doseValue = doseProfile[i].Value;
                            if (!Double.IsNaN(doseValue))
                            {
                                sum += doseProfile[i].Value;
                                count++;
                            }
                        }
                    }
                    doseProfile = null;
                }
            }
            double mean = sum / ((double)count);
            return new DoseValue(mean, doseUnit);
        }

        public class DoseMatrix
        {
            public double[,,] Matrix { get; set; }
            public double[] xValues;
            public double[] yValues;
            public double[] zValues;
            public DoseMatrix()
            {
            }

        }

        public static DoseMatrix GetDoseMatrix(Dose dose, Image image, PlanSetup plan1)
        {
            DoseMatrix doses = new DoseMatrix();
            int xSize = dose.XSize;
            int ySize = dose.YSize;
            int zSize = dose.ZSize;
            double[,,] matrix = new double[ySize, xSize, zSize];
            double xRes = dose.XRes;
            double yRes = dose.YRes;
            double zRes = dose.ZRes;
            double[] xValues = new double[xSize];
            double[] yValues = new double[ySize];
            double[] zValues = new double[zSize];
            for (int i = 0; i < xSize; i++)
            {
                xValues[i] = (dose.Origin + dose.XDirection * i * xRes).x;
            }
            for (int i = 0; i < ySize; i++)
            {
                yValues[i] = (dose.Origin + dose.YDirection * i * yRes).y;
            }
            for (int i = 0; i < zSize; i++)
            {
                zValues[i] = (dose.Origin + dose.ZDirection * i * zRes).z;
            }
            //Get the dose matrix
            for (double i = 0; i < xSize * xRes; i += xRes)
            {

                for (double j = 0; j < ySize * yRes; j += yRes)

                {
                    for (double k = 0; k < zSize * zRes; k += zRes)
                    {
                        VVector point = dose.Origin + dose.XDirection * i + dose.YDirection * j + dose.ZDirection * k;
                        point = image.DicomToUser(point, plan1);
                        matrix[(int)(j / yRes), (int)(i / xRes), (int)(k / zRes)] = dose.GetDoseToPoint(point).Dose;
                    }
                }
            }
            doses.Matrix = matrix;
            doses.xValues = xValues;
            doses.yValues = yValues;
            doses.zValues = zValues;
            return doses;

        }

        public static Tuple<List<double[,]>, string, List<Structure>> GetContours(StructureSet structureSet, PlanSetup plan1, Image image)
        {
            double meanDose = 100000;     //will be updated for each parotid with smaller mean dose.
            List<Structure> ROI = new List<Structure>();    //Saving in a list because I only have read access.
            DoseValue structDose;
            int count = 0;
            string organName = " ";
            foreach (Structure structure in structureSet.Structures)
            {
                organName = structure.Name;
                if ((organName.ToLower().Contains("par")) && !(organName.ToLower().Contains("opt")))
                {
                    //this should be a parotid... check its mean dose, use it if its the smallest.
                    structDose = CalculateMeanDose(plan1, structure);
                    if (structDose.Dose < meanDose)
                    {
                        ROI.Clear();
                        ROI.Add(structure);
                        count++;
                    }
                }
            }
            DoseValue wholeMean = CalculateMeanDose(plan1, ROI[0]);
            //MessageBox.Show(wholeMean.Dose.ToString());
            List<VVector[]> contoursTemp = new List<VVector[]>();
            //ROI is now a list with one structure; the one of interest.
            int zSlices = structureSet.Image.ZSize;

            for (int z = 0; z < zSlices; z++)
            {
                VVector[][] contoursOnPlane = ROI[0].GetContoursOnImagePlane(z);
                //If length > 1, there could be an island.
                if (contoursOnPlane.GetLength(0) > 0)
                {
                    // will check for the one with the most points, and keep that one.
                    /*int keeper = 0;
                    int numPoints = 0;
                    for (int cont = 0; cont < contoursOnPlane.GetLength(0); cont++)
                    {
                        if (contoursOnPlane[cont].GetLength(0) > numPoints)
                        {
                            keeper = cont;
                        }
                    }*/
                    contoursTemp.Add(contoursOnPlane[0]);
                }
            }
            //MessageBox.Show(contoursTemp[0][0].z.ToString());
            //Now convert this into a double[,] array list
            List<double[,]> contours = new List<double[,]>();
            for (int i = 0; i < contoursTemp.Count; i++)
            {
                contours.Add(new double[contoursTemp[i].GetLength(0), 3]);
                for (int j = 0; j < contoursTemp[i].GetLength(0); j++)
                {
                    VVector point = image.UserToDicom(contoursTemp[i][j], plan1);
                    contours[i][j, 0] = (point.x);
                    contours[i][j, 1] = (point.y);
                    contours[i][j, 2] = (point.z);
                }
            }
            contours = ClosedLooper(contours);
            contours = IslandRemover(contours);
            return Tuple.Create(contours, organName, ROI);
        }

        public static List<List<double[,]>> Chop(List<double[,]> contoursTemp, int numCutsX, int numCutsY, int numCutsZ, string organName)
        {

            //Now make the axial cuts first: 
            double[] zCuts = new double[numCutsZ];
            zCuts = BestCutZ(contoursTemp, numCutsZ);    //find out where to cut the structure along z
            List<List<double[,]>> axialDivs;
            if (numCutsZ != 0)
            {
                axialDivs = ZChop(contoursTemp, zCuts); //Perform the z cuts //holds the different sections after just the z-cuts.
            }
            else
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
            }
            else
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

            }
            else
            {
                contours = contoursY;
            }

            contours = ReOrder(contours, organName, numCutsX, numCutsY, numCutsZ);
            return contours;



        }
        public static List<List<double[,]>> ReOrder(List<List<double[,]>> contours, string organName, int numCutsX, int numCutsY, int numCutsZ)
        {
            //Reorder the organ from inferior --> superior, medial --> lateral, anterior --> posterior,
            //make 2D array which holds the mean x,y,z for each contour.
            int j = 0;
            List<List<double[,]>> finalContours = new List<List<double[,]>>();
            if (organName.ToLower().Contains("l"))    //if the left (medial --> lateral is increasing x.
            {
                for (int i = 0; i < contours.Count; i++)
                {
                    if ((i % (numCutsZ + 1) == 0) && (i != 0))
                    {
                        j += 1;
                    }
                    int index = j % ((numCutsX + 1) * (numCutsY + 1) * (numCutsZ + 1));
                    finalContours.Add(contours[index]);
                    j += (numCutsX + 1) * (numCutsY + 1);
                }
            }
            else
            {
                j = numCutsX;
                for (int i = 0; i < contours.Count; i++)
                {
                    if (j >= (numCutsX + 1) * (numCutsY + 1) * (numCutsZ + 1))
                    {
                        j -= (numCutsX + 1) * (numCutsY + 1) * (numCutsZ + 1) + 1;
                        if (j == -1)
                        {
                            j += (numCutsX + 1) * (numCutsY + 1);
                        }
                    }

                    finalContours.Add(contours[j]);
                    j += (numCutsX + 1) * (numCutsY + 1);

                }
            }
            return finalContours;
            //List<List<double[,]>> smallestY = new List<List<double[,]>>();
            //List<List<double[,]>> smallestX = new List<List<double[,]>>();
            //double[,] means;
            //double[,] meansY;  //means after taking a chunk of smallest y
            //double[,] meansZ;
            //if (organName.ToLower().Contains('l'))    //if the left (medial --> lateral is increasing x.
            //{
            //    while (input.Count != 0)
            //    {
            //        means = new double[input.Count, 3];
            //        for (int i = 0; i < input.Count; i++)
            //        {
            //            for (int j = 0; j < 3; j++)
            //            {
            //                means[i, j] = Stats.SliceMean(j, input[i]);
            //            }
            //        }
            //        smallestY.Clear();
            //        int[] smallY = Stats.LowestValIndices(means, 1, (int)(numCutsZ + 1) * (numCutsX + 1));    //Get the list of smallest y slices.
            //        for (int i = 0; i < smallY.Length; i++)
            //        {
            //            //Add the smallest y sections to the list
            //            smallestY.Add(input[smallY[i]]);
            //            //remove from beginning list
            //            input.RemoveAt(smallY[i]);
            //        }
            //        //Now from these get the smallest x.
            //        while (smallestY.Count != 0)
            //        {
            //            meansY = new double[smallestY.Count, 3];
            //            for (int i = 0; i < smallestY.Count; i++)
            //            {
            //                for (int j = 0; j < 3; j++)
            //                {
            //                    meansY[i, j] = Stats.SliceMean(j, smallestY[i]);
            //                }
            //            }
            //            smallestX.Clear();
            //            int[] smallX = Stats.LowestValIndices(meansY, 0, numCutsZ + 1);
            //            for (int i = 0; i < smallX.Length; i++)
            //            {
            //                //Add the smallest y sections to the list
            //                smallestX.Add(smallestY[smallX[i]]);
            //                smallestY.RemoveAt(smallX[i]);
            //            }

            //            while (smallestX.Count != 0)
            //            {
            //                meansZ = new double[smallestX.Count, 3];
            //                for (int i = 0; i < smallestX.Count; i++)
            //                {
            //                    for (int j = 0; j < 3; j++)
            //                    {
            //                        meansZ[i, j] = Stats.SliceMean(j, smallestX[i]);
            //                    }
            //                }
            //                int[] smallZ = Stats.LowestValIndices(meansZ, 2, 1);
            //                for (int i = 0; i < smallZ.Length; i++)
            //                {
            //                    finalContours.Add(smallestX[smallZ[0]]);
            //                    smallestX.RemoveAt(smallZ[0]);
            ////                }

            //            }
            //        }
            //    }
            //}
            //else
            //{

            //}
            //return finalContours;
        }
        public static List<List<double[,]>> XChop(List<double[,]> contours, int numCutsX)
        {
            double[] xCuts = BestCutX(contours, numCutsX, 0.0001);
            // add intersection points
            for (int i = 0; i < contours.Count; i++)
            {
                contours[i] = AddIntersectionsX(contours[i], xCuts);
                contours[i] = ClosedLooper(contours[i]);
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
                        else if (x == xCuts.Length)
                        {


                            if (contours[i][j, 0] >= xCuts[x - 1])
                            {
                                divisions[x].Add(new double[] { contours[i][j, 0], contours[i][j, 1], contours[i][j, 2] });
                            }
                        }
                        else
                        {
                            if ((contours[i][j, 0] >= xCuts[x - 1]) && (contours[i][j, 0] <= xCuts[x]))
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
                        temp = ClosedLooper(temp);
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
                    area += Area(contours[i]);
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
                            tempContours[i] = ClosedLooper(tempContours[i]);
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
                                cutContours = ClosedLooper(cutContours);
                                newArea += Area(cutContours);
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
                finalContours = AddPoint(finalContours, 0, new double[] { xCut, yNew, contours[0, 2] });
                numAdded++;
            }
            if ((contours[0, 0] < xCut) & (contours[contours.Length / 3 - 1, 0] > xCut))
            {
                m = (contours[contours.Length / 3 - 1, 0] - contours[0, 0]) / (contours[contours.Length / 3 - 1, 1] - contours[0, 1]);
                yNew = ((xCut - contours[0, 0]) / m) + contours[0, 1];
                finalContours = AddPoint(finalContours, contours.Length / 3, new double[] { xCut, yNew, contours[0, 2] });
            }

            for (int i = 0; i < contours.Length / 3 - 1; i++)    //for all points, except last one will be out of loop
            {
                if ((contours[i, 0] < xCut) & (contours[i + 1, 0] > xCut)) //if x is below the cut
                {
                    m = (contours[i + 1, 0] - contours[i, 0]) / (contours[i + 1, 1] - contours[i, 1]);
                    yNew = ((xCut - contours[i, 0]) / m) + contours[i, 1];
                    finalContours = AddPoint(finalContours, i + numAdded, new double[] { xCut, yNew, contours[0, 2] });
                    numAdded++;

                }
                else if ((contours[i, 0] > xCut) & (contours[i + 1, 0] < xCut))
                {
                    m = (contours[i + 1, 0] - contours[i, 0]) / (contours[i + 1, 1] - contours[i, 1]);
                    yNew = ((xCut - contours[i, 0]) / m) + contours[i, 1];
                    finalContours = AddPoint(finalContours, i + numAdded, new double[] { xCut, yNew, contours[0, 2] });
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
                    finalContours = AddPoint(finalContours, 0, new double[] { xCut[x], yNew, contours[0, 2] });
                    numAdded++;
                }
                if ((contours[0, 0] < xCut[x]) & (contours[contours.Length / 3 - 1, 0] > xCut[x]))
                {
                    m = (contours[contours.Length / 3 - 1, 0] - contours[0, 0]) / (contours[contours.Length / 3 - 1, 1] - contours[0, 1]);
                    yNew = ((xCut[x] - contours[0, 0]) / m) + contours[0, 1];
                    finalContours = AddPoint(finalContours, contours.Length / 3, new double[] { xCut[x], yNew, contours[0, 2] });
                }

                for (int i = 0; i < contours.Length / 3 - 1; i++)    //for all points, except last one will be out of loop
                {
                    if ((contours[i, 0] < xCut[x]) & (contours[i + 1, 0] > xCut[x])) //if y is below the cut
                    {
                        m = (contours[i + 1, 0] - contours[i, 0]) / (contours[i + 1, 1] - contours[i, 1]);
                        yNew = ((xCut[x] - contours[i, 0]) / m) + contours[i, 1];
                        finalContours = AddPoint(finalContours, i + numAdded, new double[] { xCut[x], yNew, contours[0, 2] });
                        numAdded++;

                    }
                    else if ((contours[i, 0] > xCut[x]) & (contours[i + 1, 0] < xCut[x]))
                    {
                        m = (contours[i + 1, 0] - contours[i, 0]) / (contours[i + 1, 1] - contours[i, 1]);
                        yNew = ((xCut[x] - contours[i, 0]) / m) + contours[i, 1];
                        finalContours = AddPoint(finalContours, i + numAdded, new double[] { xCut[x], yNew, contours[0, 2] });
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
                contours[i] = ClosedLooper(contours[i]);
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
                finalContours.Add(new List<double[,]>());
            }


            for (int i = 0; i < contours.Count; i++)    //for all of the contours
            {
                divisions.Clear();
                //Make the list the correct size so that there is an item for each y division.
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
                            if (contours[i][j, 1] <= yCuts[y])
                            {
                                divisions[y].Add(new double[] { contours[i][j, 0], contours[i][j, 1], contours[i][j, 2] });

                            }
                        }
                        else if (y == yCuts.Length)
                        {


                            if (contours[i][j, 1] >= yCuts[y - 1])
                            {
                                divisions[y].Add(new double[] { contours[i][j, 0], contours[i][j, 1], contours[i][j, 2] });
                            }
                        }
                        else
                        {
                            if ((contours[i][j, 1] >= yCuts[y - 1]) && (contours[i][j, 1] <= yCuts[y]))
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
                        temp[row, 1] = divisions[y][row][1];
                        temp[row, 2] = divisions[y][row][2];
                    }

                    if (temp.Length != 0)
                    {
                        temp = ClosedLooper(temp);
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
                    area += Area(contours[i]);
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
                            if (contours[j][row, 1] > maxY)
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
                            tempContours[i] = ClosedLooper(tempContours[i]);
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
                                cutContours = ClosedLooper(cutContours);
                                newArea += Area(cutContours);
                            }
                        }
                    }
                    //Now compare areas:
                    if (newArea / area < areaGoal)
                    {
                        minY = yCut;
                    }
                    else if (newArea / area > areaGoal)
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
            if ((contours[0, 1] > yCut) & (contours[contours.Length / 3 - 1, 1] < yCut))
            {
                m = (contours[0, 1] - contours[contours.Length / 3 - 1, 1]) / (contours[0, 0] - contours[contours.Length / 3 - 1, 0]);
                xNew = ((yCut - contours[contours.Length / 3 - 1, 1]) / m) + contours[contours.Length / 3 - 1, 0];
                finalContours = AddPoint(finalContours, 0, new double[] { xNew, yCut, contours[0, 2] });
                numAdded++;
            }
            if ((contours[0, 1] < yCut) & (contours[contours.Length / 3 - 1, 1] > yCut))
            {
                m = (contours[contours.Length / 3 - 1, 1] - contours[0, 1]) / (contours[contours.Length / 3 - 1, 0] - contours[0, 0]);
                xNew = ((yCut - contours[0, 1]) / m) + contours[0, 0];
                finalContours = AddPoint(finalContours, contours.Length / 3, new double[] { xNew, yCut, contours[0, 2] });
            }

            for (int i = 1; i < contours.Length / 3 - 1; i++)    //for all points, except last one will be out of loop
            {
                if ((contours[i, 1] < yCut) & (contours[i + 1, 1] > yCut)) //if y is below the cut
                {
                    m = (contours[i + 1, 1] - contours[i, 1]) / (contours[i + 1, 0] - contours[i, 0]);
                    xNew = ((yCut - contours[i, 1]) / m) + contours[i, 0];
                    finalContours = AddPoint(finalContours, i + numAdded, new double[] { xNew, yCut, contours[0, 2] });
                    numAdded++;

                }
                else if ((contours[i, 1] > yCut) & (contours[i + 1, 1] < yCut))
                {
                    m = (contours[i + 1, 1] - contours[i, 1]) / (contours[i + 1, 0] - contours[i, 0]);
                    xNew = ((yCut - contours[i, 1]) / m) + contours[i, 0];
                    finalContours = AddPoint(finalContours, i + numAdded, new double[] { xNew, yCut, contours[0, 2] });
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
                    finalContours = AddPoint(finalContours, 0, new double[] { xNew, yCut[y], contours[0, 2] });
                    numAdded++;
                }
                if ((contours[0, 1] < yCut[y]) & (contours[contours.Length / 3 - 1, 1] > yCut[y]))
                {
                    m = (contours[contours.Length / 3 - 1, 1] - contours[0, 1]) / (contours[contours.Length / 3 - 1, 0] - contours[0, 0]);
                    xNew = ((yCut[y] - contours[0, 1]) / m) + contours[0, 0];
                    finalContours = AddPoint(finalContours, contours.Length / 3, new double[] { xNew, yCut[y], contours[0, 2] });
                }
                for (int i = 1; i < numConts - 1; i++)    //for all points, except last one will be out of loop
                {
                    if ((contours[i, 1] < yCut[y]) & (contours[i + 1, 1] > yCut[y])) //if y is below the cut
                    {
                        m = (contours[i + 1, 1] - contours[i, 1]) / (contours[i + 1, 0] - contours[i, 0]);
                        xNew = ((yCut[y] - contours[i, 1]) / m) + contours[i, 0];
                        finalContours = AddPoint(finalContours, i + numAdded, new double[] { xNew, yCut[y], contours[0, 2] });
                        numAdded++;
                    }
                    else if ((contours[i, 1] > yCut[y]) & (contours[i + 1, 1] < yCut[y]))
                    {
                        m = (contours[i + 1, 1] - contours[i, 1]) / (contours[i + 1, 0] - contours[i, 0]);
                        xNew = ((yCut[y] - contours[i, 1]) / m) + contours[i, 0];
                        finalContours = AddPoint(finalContours, i + numAdded, new double[] { xNew, yCut[y], contours[0, 2] });
                        numAdded++;
                    }
                }
            }
            return finalContours;
        }
        public static double[] BestCutZ(List<double[,]> contours, int numCuts)
        {
            double[] zCuts = new double[numCuts];    //to hold z value of cut locations
            int numConts = contours.Count;
            double totalVolume = 0;
            double deltaZ = Math.Abs(contours[0][0, 2] - contours[1][0, 2]);    //distance between adjacent contours
            double[] contourAreas = new double[numConts];

            for (int i = 0; i < numConts; i++) //right now using area of every contour but last... should last be included? 
            {
                contourAreas[i] = Area(contours[i]);
                if (i != numConts - 1)
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
                avgArea = 0.5 * (contourAreas[contIndex - 1] + contourAreas[contIndex]);


                zCuts[i - 1] = contours[contIndex - 1][0, 2] + (volumeGoal * totalVolume - volumeBelow) / avgArea;
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
                newContour = new double[numPoints, 3];

                for (int j = 0; j < numPoints; j++)
                {
                    double x = contours[contoursZ[0]][j, 0];
                    double y = contours[contoursZ[0]][j, 1];
                    double z = contours[contoursZ[0]][j, 2];
                    //Now get idx, the row index in the second closest contour. Then interpolate between the two.
                    int idx = ClosestPoint(x, y, contours[contoursZ[1]]);
                    double[] point1 = { x, y, z };
                    double[] point2 = { contours[contoursZ[1]][idx, 0], contours[contoursZ[1]][idx, 1], contours[contoursZ[1]][idx, 2] };
                    double[] newPoint = InterpolateXY(point1, point2, zCuts[i]);
                    //now add newPoint to the newContour:
                    newContour[j, 0] = newPoint[0];
                    newContour[j, 1] = newPoint[1];
                    newContour[j, 2] = newPoint[2];
                }
                //Add this new contour to the list.
                sliceIndices[i] = Math.Max(contoursZ[0], contoursZ[1]);
                contours.Insert(sliceIndices[i], newContour);
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
                }
                else if (cut > 0 & cut < zCuts.Length)
                {
                    for (int i = sliceIndices[cut - 1]; i <= sliceIndices[cut]; i++)
                    {
                        tempList.Add(contours[i]);
                    }

                }
                else
                {
                    for (int i = sliceIndices[cut - 1]; i <= contours.Count - 1; i++)
                    {
                        tempList.Add(contours[i]);
                    }
                }
                finalContours.Add(tempList.GetRange(0, tempList.Count));
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
                double diff = Math.Sqrt(Math.Pow((x - points[i, 0]), 2) + Math.Pow((y - points[i, 1]), 2));    //difference between points in xy plane
                if (diff < m)
                {
                    closestPoint = i;
                    m = diff;
                }
            }
            if (closestPoint == 1000)
            {
                MessageBox.Show("Closest Point not found, terminating.");

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
        public static double[] ReverseArray(double[] array)
        {
            double[] newArray = new double[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                newArray[i] = array[array.Length - 1 - i];
            }
            return newArray;
        }
        public static int[] ReverseArray(int[] array)
        {
            int[] newArray = new int[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                newArray[i] = array[array.Length - 1 - i];
            }
            return newArray;
        }
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
                for (int j = 0; j < a.GetLength(0); j++)
                {
                    if ((a[j, dim] < min[0]) && (!used.Contains(j)))
                    {
                        min[0] = a[j, dim];
                        min[1] = j;
                    }
                }
                smalls[i] = (int)min[1];
                used.Add(smalls[i]);
                min[0] = 1000;
                min[1] = 0;
            }
            smalls = Sort(smalls);
            smalls = ReverseArray(smalls);
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
        public static double[,] MeanDoses(List<List<double[,]>> contours, DoseMatrix dose, int SSFactor, int SSFactorZ, string organName)
        {


            double[,,] organDoseBounds = OrganDoseBounds(contours, dose);
            DoseMatrix doseSS = DoseMatrixSuperSampler(dose, organDoseBounds, SSFactor, SSFactorZ);

            //Find mean doses:
            double[,] meanDoses = new double[contours.Count + 1, 2];    //second column for # of dose voxels in region. Final row for whole mean
            double x, y, z, minY, maxY, minX, maxX;
            double[,] polygon;
            double[] point = new double[3]; //the interpolated contour of the dose voxel z position.
            int numIn = 0;
            for (int i = 0; i < doseSS.Matrix.GetLength(0); i++)
            {
                for (int j = 0; j < doseSS.Matrix.GetLength(1); j++)
                {
                    for (int k = 0; k < doseSS.Matrix.GetLength(2); k++)
                    {
                        //get coordinates for the dose voxel
                        x = doseSS.xValues[j];
                        y = doseSS.yValues[i];
                        z = doseSS.zValues[k];

                        //Determine potential subregions for the voxel:
                        for (int region = 0; region < contours.Count; region++)
                        {
                            for (int cont = 0; cont < contours[region].Count - 1; cont++)
                            {
                                if ((contours[region][cont][0, 2] <= z) && (contours[region][cont + 1][0, 2] >= z))    //Is this point within z range of this region?
                                {
                                    //is point within max bounds of the region? 
                                    for (int idx = cont; idx <= cont + 1; idx++)    //for both these contours that z is between 
                                    {
                                        minX = min(contours[region][idx], 0);
                                        minY = min(contours[region][idx], 1);
                                        maxX = max(contours[region][idx], 0);
                                        maxY = max(contours[region][idx], 1);
                                        if ((x > minX) && (x < maxX) && (y > minY) && (y < maxY))
                                        {
                                            //Now interpolate a contour between the two.
                                            polygon = InterpBetweenContours(contours[region][cont], contours[region][cont + 1], z);

                                            //Now point in polygon.
                                            point[0] = x;
                                            point[1] = y;
                                            point[2] = z;
                                            if (PointInPolygon(polygon, point))
                                            {
                                                meanDoses[region, 0] += doseSS.Matrix[i, j, k];
                                                meanDoses[region, 1]++;
                                                numIn++;
                                            }
                                        }
                                    }
                                }
                            }
                        }


                    }
                }
            }

            double wholeMean = 0;
            int totalPoints = 0;
            for (int i = 0; i < meanDoses.GetLength(0); i++)
            {
                wholeMean += meanDoses[i, 0];
                meanDoses[i, 0] /= meanDoses[i, 1];
                totalPoints += (int)meanDoses[i, 1];
            }
            wholeMean /= totalPoints;
            meanDoses[meanDoses.GetLength(0) - 1, 0] = wholeMean;
            meanDoses[meanDoses.GetLength(0) - 1, 1] = totalPoints;
            string output = "";
            string beginSpace = "         ";    //how much space between columns?
            string middleSpace = "     ";
            output += "Regional mean doses (cGy):" + System.Environment.NewLine;
            output += "SubRegion:  |  Dose:" + System.Environment.NewLine;
            output += "--------------------" + System.Environment.NewLine;

            for (int i = 0; i < meanDoses.GetLength(0) - 1; i++)
            {
                if (i == 10)
                {
                    beginSpace = "        ";
                }
                output += beginSpace + (i + 1) + middleSpace + String.Format("{0:0.00}", meanDoses[i, 0]) + System.Environment.NewLine;
                output += "--------------------" + System.Environment.NewLine;
            }

            output += "Whole Mean Dose:    " + String.Format("{0:0.00}", meanDoses[meanDoses.GetLength(0) - 1, 0]);

            //Make sure regions in correct order:
            output += System.Environment.NewLine + "Correct Order test: ";
            bool correctOrder = RegionOrderingTest18(contours, organName);
            if (correctOrder)
            {
                output += "Passed";
            }else
            {
                output += "Failed";
            }

            MessageBox.Show(output);


            return meanDoses;
        }
        public static DoseMatrix DoseMatrixSuperSampler(DoseMatrix dose, double[,,] organDoseBounds, int SSFactor, int SSFactorZ)
        {

            //First get the number of matrix elements in each direction encompassing the organ
            int xRange = (int)(organDoseBounds[0, 1, 1] - organDoseBounds[0, 1, 0] + 1);
            int yRange = (int)(organDoseBounds[1, 1, 1] - organDoseBounds[1, 1, 0] + 1);
            int zRange = (int)(organDoseBounds[2, 1, 1] - organDoseBounds[2, 1, 0] + 1);
            //Now get the z,y,z values for the supersampled matrix.
            double[] xValues = new double[xRange * SSFactor];
            double[] yValues = new double[yRange * SSFactor];
            double[] zValues = new double[zRange * SSFactorZ];
            //Get the distance between dose pixels
            double deltaX = dose.xValues[2] - dose.xValues[1];
            double deltaY = dose.yValues[2] - dose.yValues[1];
            double deltaZ = dose.zValues[2] - dose.zValues[1];
            for (int i = 0; i < xRange * SSFactor; i++)
            {
                xValues[i] = dose.xValues[(int)organDoseBounds[0, 1, 0]] + (i) * deltaX / SSFactor;
            }
            for (int i = 0; i < yRange * SSFactor; i++)
            {
                yValues[i] = dose.yValues[(int)organDoseBounds[1, 1, 0]] + (i) * deltaY / SSFactor;
            }
            for (int i = 0; i < zRange * SSFactorZ; i++)
            {
                zValues[i] = dose.zValues[(int)organDoseBounds[2, 1, 0]] + (i) * deltaZ / SSFactorZ;
            }
            //Now make a new dose array
            double[,,] doseSS = new double[yValues.Length, xValues.Length, zValues.Length];
            double[,,] test = new double[yRange, xRange, zRange];
            //First get the original values in
            for (int i = 0; i < xRange; i++)
            {
                for (int j = 0; j < yRange; j++)
                {
                    for (int k = 0; k < zRange; k++)
                    {
                        doseSS[j * SSFactor, i * SSFactor, k * SSFactorZ] =
                            dose.Matrix[j + (int)organDoseBounds[1, 1, 0], i + (int)organDoseBounds[0, 1, 0], k + (int)organDoseBounds[2, 1, 0]];
                        test[j, i, k] = dose.Matrix[j + (int)organDoseBounds[1, 1, 0], i + (int)organDoseBounds[0, 1, 0], k + (int)organDoseBounds[2, 1, 0]];
                    }
                }
            }

            if (SSFactor != 1)
            {
                //Now supersample along x:
                for (int k = 0; k < zRange; k++)
                {
                    for (int j = 0; j < yRange; j++)
                    {
                        for (int i = 0; i < xRange - 1; i++)
                        {
                            for (int insert = 1; insert <= SSFactor - 1; insert++)
                            {
                                doseSS[j * SSFactor, i * SSFactor + insert, k * SSFactorZ] =
                                    doseSS[j * SSFactor, i * SSFactor, SSFactorZ * k] + ((double)insert / SSFactor) *
                                    (doseSS[j * SSFactor, SSFactor * (i + 1), k * SSFactorZ] - doseSS[j * SSFactor, i * SSFactor, k * SSFactorZ]);
                            }
                        }
                    }
                }
                //Now supersample along y:
                for (int k = 0; k < zRange; k++)
                {
                    for (int i = 0; i < xRange * SSFactor; i++)
                    {
                        for (int j = 0; j < yRange - 1; j++)
                        {
                            for (int insert = 1; insert <= SSFactor - 1; insert++)
                            {
                                doseSS[j * SSFactor + insert, i, k * SSFactorZ] = doseSS[j * SSFactor, i, k * SSFactorZ] + ((double)insert / SSFactor) *
                                    (doseSS[(j + 1) * SSFactor, i, k * SSFactorZ] - doseSS[j * SSFactor, i, k * SSFactorZ]);
                            }
                        }
                    }
                }
                if (SSFactorZ != 1)
                {
                    //Now supersample along Z:
                    for (int j = 0; j < yRange * SSFactor; j++)
                    {
                        for (int i = 0; i < xRange * SSFactor; i++)
                        {
                            for (int k = 0; k < zRange - 1; k++)
                            {
                                for (int insert = 1; insert <= SSFactorZ - 1; insert++)
                                {
                                    doseSS[j, i, k * SSFactorZ + insert] = doseSS[j, i, k * SSFactorZ] + ((double)insert / SSFactorZ) *
                                        (doseSS[j, i, SSFactorZ * (k + 1)] - doseSS[j, i, SSFactorZ * k]);
                                }
                            }
                        }
                    }
                }
            }

            //Now need to make a new dose matrix object with this data.
            DoseMatrix newMatrix = new DoseMatrix();
            newMatrix.Matrix = doseSS;
            newMatrix.xValues = xValues;
            newMatrix.yValues = yValues;
            newMatrix.zValues = zValues;
            return newMatrix;
        }

        public static double[,,] OrganDoseBounds(List<List<double[,]>> contours, DoseMatrix dose)
        //this gives a 2D array, with the first layer, first column giving the min x,y,z bounds of the organs
        //first layer second column gives the min x,y,z indices in the dose matrix,
        //second layer is the same except for the max. 
        {
            int col = 0;
            int row = 0;
            double[,,] organDoseBounds = new double[3, 2, 2];
            //Set inital values which will be replaced by max, mins.
            for (int i = 0; i < 6; i++)
            {
                if (i == 3)
                {
                    col++;
                    row = 0;
                }
                organDoseBounds[row, col, 0] = 1000;
                row++;
            }
            col = 0;
            row = 0;
            for (int i = 0; i < 6; i++)
            {
                if (i == 3)
                {
                    col++;
                    row = 0;
                }
                organDoseBounds[row, col, 1] = -1000;
                row++;
            }

            for (int i = 0; i < contours.Count; i++)
            {
                for (int j = 0; j < contours[i].Count; j++)
                {
                    for (int k = 0; k < contours[i][j].Length / 3; k++)
                    {
                        //get min bounds
                        if (contours[i][j][k, 0] < organDoseBounds[0, 0, 0])
                        {
                            organDoseBounds[0, 0, 0] = contours[i][j][k, 0];
                        }
                        else if (contours[i][j][k, 1] < organDoseBounds[1, 0, 0])
                        {
                            organDoseBounds[1, 0, 0] = contours[i][j][k, 1];
                        }
                        else if (contours[i][j][k, 2] < organDoseBounds[2, 0, 0])
                        {
                            organDoseBounds[2, 0, 0] = contours[i][j][k, 2];
                        }
                        //Get max bounds
                        if (contours[i][j][k, 0] > organDoseBounds[0, 0, 1])
                        {
                            organDoseBounds[0, 0, 1] = contours[i][j][k, 0];
                        }
                        else if (contours[i][j][k, 1] > organDoseBounds[1, 0, 1])
                        {
                            organDoseBounds[1, 0, 1] = contours[i][j][k, 1];
                        }
                        else if (contours[i][j][k, 2] > organDoseBounds[2, 0, 1])
                        {
                            organDoseBounds[2, 0, 1] = contours[i][j][k, 2];
                        }
                    }
                }
            }
            //Now find what indices in the dose files encapsulate these bounds.
            //Do this by finding min value that is between -2.5 and 0mm below minimum,
            //And max value between 0 and 2.5mm above max.
            for (int x = 0; x < dose.xValues.Length; x++)
            {
                if ((organDoseBounds[0, 0, 0] - dose.xValues[x] > 0) && (organDoseBounds[0, 0, 0] - dose.xValues[x] <= 2.5))
                {
                    organDoseBounds[0, 1, 0] = x;
                }
                if ((dose.xValues[x] - organDoseBounds[0, 0, 1] > 0) && (dose.xValues[x] - organDoseBounds[0, 0, 1] <= 2.5))
                {
                    organDoseBounds[0, 1, 1] = x;
                }
            }
            for (int y = 0; y < dose.yValues.Length; y++)
            {
                if ((organDoseBounds[1, 0, 0] - dose.yValues[y] > 0) && (organDoseBounds[1, 0, 0] - dose.yValues[y] <= 2.5))
                {
                    organDoseBounds[1, 1, 0] = y;
                }
                if ((dose.yValues[y] - organDoseBounds[1, 0, 1] > 0) && (dose.yValues[y] - organDoseBounds[1, 0, 1] <= 2.5))
                {
                    organDoseBounds[1, 1, 1] = y;
                }
            }
            for (int z = 0; z < dose.zValues.Length; z++)
            {
                if ((organDoseBounds[2, 0, 0] - dose.zValues[z] > 0) && (organDoseBounds[2, 0, 0] - dose.zValues[z] <= 2.5))
                {
                    organDoseBounds[2, 1, 0] = z;
                }
                if ((dose.zValues[z] - organDoseBounds[2, 0, 1] > 0) && (dose.zValues[z] - organDoseBounds[2, 0, 1] <= 2.5))
                {
                    organDoseBounds[2, 1, 1] = z;
                }
            }
            return organDoseBounds;
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
            int j = polygon.GetLength(0) - 1;
            for (int i = 0; i < polygon.GetLength(0); i++)
            {
                if (((polygon[i, 1] < point[1]) && (polygon[j, 1] >= point[1])) || ((polygon[i, 1] >= point[1]) && (polygon[j, 1] < point[1])))
                {
                    if (polygon[i, 0] + (point[1] - polygon[i, 1]) / (polygon[j, 1] - polygon[i, 1]) * (polygon[j, 0] - polygon[i, 0]) < point[0])
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;

        }


        public static double[,] InterpBetweenContours(double[,] a, double[,] b, double zVal)
        //interpolate between contours a and b at the specified z value. (z is between the contours)
        {
            //First check if either contour is at zVal exactly: 
            if (a[0, 2] == zVal)
            {
                return a;
            }
            else if (b[0, 2] == zVal)
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
                double[] point2 = { b[idx, 0], b[idx, 1], b[idx, 2] };
                double[] newPoint = InterpolateXY(point1, point2, zVal);
                c[i, 0] = newPoint[0];
                c[i, 1] = newPoint[1];
                c[i, 2] = newPoint[2];
            }
            return c;
        }
        public static List<double[,]> IslandRemover(List<double[,]> contours)
        //This function will remove ROI contour islands if they exist.
        //Basic idea is to search through contour points, and if there is a large
        //variation in x or y from one contour to the next, then remove the contour which is 
        //furthest from the mean.
        {
            int numIslands = 0;
            int numContours = contours.Count;
            double meanX = 0;
            double meanY = 0;
            int maxSep = 30; //island cutoff criteria (mm), difference in means between adjacent contours (X,y)
            List<double> means = new List<double>();


            //first get the mean x,y,z for the whole ROI:
            meanX = SliceMean(0, contours);
            meanY = SliceMean(1, contours);
            means.Add(meanX);
            means.Add(meanY);


            //Now go through and check for large variation between adjacent contours: 
            //Currently using a difference of 2cm means between adjacent contours to flag an island
            for (int i = 0; i < numContours - 2; i++)
            {
                //Another for loop to check both x and y columns:
                for (int col = 0; col < 2; col++)
                {
                    double mean1 = SliceMean(col, contours[i]);
                    double mean2 = SliceMean(col, contours[i + 1]);
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

            }
            else if (numIslands == 1)
            {
                MessageBox.Show(numIslands + " island detected and removed");
            }
            else
            {
                MessageBox.Show(numIslands + " islands detected and removed");
            }
            return contours;
        }
        public static List<double[,]> ClosedLooper(List<double[,]> contours)
        //Here we ensure that each contour forms a closed loop. 
        {

            for (int i = 0; i < contours.Count; i++)
            {
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

                b[row, column] = a[row, column];

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
                for (int j = index * 3; j < b.Length - 3; j++)
                {
                    int column = j % 3;

                    b[row, column] = a[row - 1, column];

                    if (column == 2)
                    {
                        row++;
                    }
                }
            }

            return b;
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
            }
            else
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
                yVals.Add(SliceMean(1, temp[i])); //add average y value for each region
            }
            yVals.Sort();
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (SliceMean(1, temp[i]) == yVals[j])
                    {
                        tempY.Add(temp[i]);
                        tempYIndices.Add(tempIndices[i]);
                        break;
                    }
                }
                for (int j = 3; j < 6; j++)
                {
                    if (SliceMean(1, temp[i]) == yVals[j])
                    {
                        tempY2.Add(temp[i]);
                        tempY2Indices.Add(tempIndices[i]);
                        break;
                    }
                }
            }

            for (int i = 0; i < 3; i++)
            {
                xVals.Add(SliceMean(0, tempY[i])); //add average y value for each region
            }
            for (int i = 0; i < 3; i++)
            {
                xVals2.Add(SliceMean(0, tempY2[i])); //add average y value for each region
            }
            xVals.Sort();
            xVals2.Sort();
            if (organSide == "l")
            {
                for (int i = 0; i < 3; i++)
                {
                    if (SliceMean(0, tempY[i]) == xVals[0])
                    {
                        actualOrder[tempYIndices[i]] = "caudal - anterior - medial";

                    }
                    if (SliceMean(0, tempY[i]) == xVals[1])
                    {
                        actualOrder[tempYIndices[i]] = "caudal - anterior - middle";

                    }
                    if (SliceMean(0, tempY[i]) == xVals[2])
                    {
                        actualOrder[tempYIndices[i]] = "caudal - anterior - lateral";

                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    if (SliceMean(0, tempY2[i]) == xVals2[0])
                    {
                        actualOrder[tempY2Indices[i]] = "caudal - posterior - medial";

                    }
                    if (SliceMean(0, tempY2[i]) == xVals2[1])
                    {
                        actualOrder[tempY2Indices[i]] = "caudal - posterior - middle";

                    }
                    if (SliceMean(0, tempY2[i]) == xVals2[2])
                    {
                        actualOrder[tempY2Indices[i]] = "caudal - posterior - lateral";

                    }
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    if (SliceMean(0, tempY[i]) == xVals[0])
                    {
                        actualOrder[tempYIndices[i]] = "caudal - anterior - lateral";

                    }
                    if (SliceMean(0, tempY[i]) == xVals[1])
                    {
                        actualOrder[tempYIndices[i]] = "caudal - anterior - middle";

                    }
                    if (SliceMean(0, tempY[i]) == xVals[2])
                    {
                        actualOrder[tempYIndices[i]] = "caudal - anterior - medial";

                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    if (SliceMean(0, tempY2[i]) == xVals2[0])
                    {
                        actualOrder[tempY2Indices[i]] = "caudal - posterior - lateral";

                    }
                    if (SliceMean(0, tempY2[i]) == xVals2[1])
                    {
                        actualOrder[tempY2Indices[i]] = "caudal - posterior - middle";

                    }
                    if (SliceMean(0, tempY2[i]) == xVals2[2])
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
                yVals.Add(SliceMean(1, temp[i])); //add average y value for each region
            }
            yVals.Sort();
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (SliceMean(1, temp[i]) == yVals[j])
                    {
                        tempY.Add(temp[i]);
                        tempYIndices.Add(tempIndices[i]);
                        break;
                    }
                }
                for (int j = 3; j < 6; j++)
                {
                    if (SliceMean(1, temp[i]) == yVals[j])
                    {
                        tempY2.Add(temp[i]);
                        tempY2Indices.Add(tempIndices[i]);
                        break;
                    }
                }
            }

            for (int i = 0; i < 3; i++)
            {
                xVals.Add(SliceMean(0, tempY[i])); //add average y value for each region
            }
            for (int i = 0; i < 3; i++)
            {
                xVals2.Add(SliceMean(0, tempY2[i])); //add average y value for each region
            }
            xVals.Sort();
            xVals2.Sort();
            if (organSide == "l")
            {
                for (int i = 0; i < 3; i++)
                {
                    if (SliceMean(0, tempY[i]) == xVals[0])
                    {
                        actualOrder[tempYIndices[i]] = "middle - anterior - medial";

                    }
                    if (SliceMean(0, tempY[i]) == xVals[1])
                    {
                        actualOrder[tempYIndices[i]] = "middle - anterior - middle";

                    }
                    if (SliceMean(0, tempY[i]) == xVals[2])
                    {
                        actualOrder[tempYIndices[i]] = "middle - anterior - lateral";

                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    if (SliceMean(0, tempY2[i]) == xVals2[0])
                    {
                        actualOrder[tempY2Indices[i]] = "middle - posterior - medial";

                    }
                    if (SliceMean(0, tempY2[i]) == xVals2[1])
                    {
                        actualOrder[tempY2Indices[i]] = "middle - posterior - middle";

                    }
                    if (SliceMean(0, tempY2[i]) == xVals2[2])
                    {
                        actualOrder[tempY2Indices[i]] = "middle - posterior - lateral";

                    }
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    if (SliceMean(0, tempY[i]) == xVals[0])
                    {
                        actualOrder[tempYIndices[i]] = "middle - anterior - lateral";

                    }
                    if (SliceMean(0, tempY[i]) == xVals[1])
                    {
                        actualOrder[tempYIndices[i]] = "middle - anterior - middle";

                    }
                    if (SliceMean(0, tempY[i]) == xVals[2])
                    {
                        actualOrder[tempYIndices[i]] = "middle - anterior - medial";

                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    if (SliceMean(0, tempY2[i]) == xVals2[0])
                    {
                        actualOrder[tempY2Indices[i]] = "middle - posterior - lateral";

                    }
                    if (SliceMean(0, tempY2[i]) == xVals2[1])
                    {
                        actualOrder[tempY2Indices[i]] = "middle - posterior - middle";

                    }
                    if (SliceMean(0, tempY2[i]) == xVals2[2])
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
                yVals.Add(SliceMean(1, temp[i])); //add average y value for each region
            }
            yVals.Sort();
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (SliceMean(1, temp[i]) == yVals[j])
                    {
                        tempY.Add(temp[i]);
                        tempYIndices.Add(tempIndices[i]);
                        break;
                    }
                }
                for (int j = 3; j < 6; j++)
                {
                    if (SliceMean(1, temp[i]) == yVals[j])
                    {
                        tempY2.Add(temp[i]);
                        tempY2Indices.Add(tempIndices[i]);
                        break;
                    }
                }
            }

            for (int i = 0; i < 3; i++)
            {
                xVals.Add(SliceMean(0, tempY[i])); //add average y value for each region
            }
            for (int i = 0; i < 3; i++)
            {
                xVals2.Add(SliceMean(0, tempY2[i])); //add average y value for each region
            }
            xVals.Sort();
            xVals2.Sort();
            if (organSide == "l")
            {
                for (int i = 0; i < 3; i++)
                {
                    if (SliceMean(0, tempY[i]) == xVals[0])
                    {
                        actualOrder[tempYIndices[i]] = "superior - anterior - medial";

                    }
                    if (SliceMean(0, tempY[i]) == xVals[1])
                    {
                        actualOrder[tempYIndices[i]] = "superior - anterior - middle";

                    }
                    if (SliceMean(0, tempY[i]) == xVals[2])
                    {
                        actualOrder[tempYIndices[i]] = "superior - anterior - lateral";

                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    if (SliceMean(0, tempY2[i]) == xVals2[0])
                    {
                        actualOrder[tempY2Indices[i]] = "superior - posterior - medial";

                    }
                    if (SliceMean(0, tempY2[i]) == xVals2[1])
                    {
                        actualOrder[tempY2Indices[i]] = "superior - posterior - middle";

                    }
                    if (SliceMean(0, tempY2[i]) == xVals2[2])
                    {
                        actualOrder[tempY2Indices[i]] = "superior - posterior - lateral";

                    }
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    if (SliceMean(0, tempY[i]) == xVals[0])
                    {
                        actualOrder[tempYIndices[i]] = "superior - anterior - lateral";

                    }
                    if (SliceMean(0, tempY[i]) == xVals[1])
                    {
                        actualOrder[tempYIndices[i]] = "superior - anterior - middle";

                    }
                    if (SliceMean(0, tempY[i]) == xVals[2])
                    {
                        actualOrder[tempYIndices[i]] = "superior - anterior - medial";

                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    if (SliceMean(0, tempY2[i]) == xVals2[0])
                    {
                        actualOrder[tempY2Indices[i]] = "superior - posterior - lateral";

                    }
                    if (SliceMean(0, tempY2[i]) == xVals2[1])
                    {
                        actualOrder[tempY2Indices[i]] = "superior - posterior - middle";

                    }
                    if (SliceMean(0, tempY2[i]) == xVals2[2])
                    {
                        actualOrder[tempY2Indices[i]] = "superior - posterior - medial";

                    }
                }
            }

            if (actualOrder.SequenceEqual(correctOrder))
            {
                return true;
            }
            else
            {
                return false;
            }

        }



    }






}

