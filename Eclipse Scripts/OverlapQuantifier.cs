using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Shapes;
using System.Windows.Media;
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

            //Now get the contours. Get the contralateral parotid as parotid with the smallest dose.
            StructureSet structureSet = context.StructureSet;
            var tuple = GetContours(structureSet, plan1, image);

            //returns contours and parotid name.
            List<double[,]> contours = tuple.Item1;
            string organName = tuple.Item2;
            List<Structure> ROI = tuple.Item3;

            List<List<double[,]>> choppedContours = Chop(contours, numCutsX, numCutsY, numCutsZ, organName);

            List<Structure> PTVs = GetStructuresPTV(structureSet, plan1, image);

            //returns contours and parotid name.
            var tuple2 = OverlapValues(choppedContours, PTVs, ROI);

            bool[] overlapValues = tuple2.Item1;
            double overlapPercent = tuple2.Item2;
            
            //make CSV for meandoses
            string fileName = "Overlap.csv";
            string path = System.IO.Path.Combine(@"\\phsabc.ehcnet.ca\HomeDir\HomeDir02\csample1\Profile\Desktop\Parotid Project\Base Planning\meanDoses", testPatient.LastName);
            using (StreamWriter outputFile = new StreamWriter(System.IO.Path.Combine(path, fileName)))
            {
                for (int i = 0; i < overlapValues.GetLength(0); i++)
                {
                    outputFile.WriteLine((i + 1) + ", " + String.Format("{0:0}", Convert.ToInt32(overlapValues[i])));
                }
                outputFile.WriteLine("Overlap Percent, " + String.Format("{0:0.0}", overlapPercent));
            }
            
        }


        public static Tuple<List<double[,]>, string, List<Structure>> GetContours(StructureSet structureSet, PlanSetup plan1, Image image)
        {
            double meanDose = 100000;     //will be updated for each parotid with smaller mean dose.
            List<Structure> ROI = new List<Structure>();
            List<Structure> ROI_opt = new List<Structure>();

            double structDose;
            int count = 0;
            string organName = " ";
            string name = "";
            foreach (Structure structure in structureSet.Structures)
            {
                organName = structure.Name;
                if ((organName.ToLower().Contains("par")) && !(organName.ToLower().Contains("opt")))
                {
                    //this should be a parotid... check its mean dose, use it if its the smallest.
                    DVHData dvh = plan1.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.01);
                    structDose = dvh.MeanDose.Dose;
                    if (structDose < meanDose)
                    {
                        ROI.Clear();
                        ROI.Add(structure);
                        count++;
                        meanDose = structDose;
                        name = structure.Name;

                    }
                }
            }
            //Get the opti version too
            foreach (Structure structure in structureSet.Structures)
            {
                organName = structure.Name;
                if ((organName.ToLower().Contains("par")) && (organName.ToLower().Contains("opt")))
                {
                    //this should be a parotid... check its mean dose, use it if its the smallest.
                    DVHData dvh = plan1.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.01);
                    structDose = dvh.MeanDose.Dose;
                    if (structDose < meanDose)
                    {
                        ROI_opt.Clear();
                        ROI_opt.Add(structure);
                        meanDose = structDose;
                        name = structure.Name;

                    }
                }
            }
            try
            {
                ROI.Add(ROI_opt[0]);
            }catch
            {
                ROI.Add(ROI[0]);
            }

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
            return Tuple.Create(contours, name, ROI);
        }

        public static List<List<double[,]>> GetContoursPTV(StructureSet structureSet, PlanSetup plan1, Image image)
        {

            List<Structure> ROI = new List<Structure>();    //Saving in a list because I only have read access.
            string organName = "";
            List<VVector[]> contoursTemp = new List<VVector[]>();
            foreach (Structure structure in structureSet.Structures)
            {
                organName = structure.Name;
                if (organName.ToLower().Contains("ptv"))
                {
                    ROI.Add(structure);

                }
            }
            List<List<double[,]>> contours = new List<List<double[,]>>();
            for (int l = 0; l < ROI.Count; l++)
            {
                contours.Add(new List<double[,]>());
            }
            for (int contourCount = 0; contourCount < ROI.Count; contourCount++)
            {
                contoursTemp.Clear();
                //ROI is now a list with one structure; the one of interest.
                int zSlices = structureSet.Image.ZSize;

                for (int z = 0; z < zSlices; z++)
                {
                    VVector[][] contoursOnPlane = ROI[contourCount].GetContoursOnImagePlane(z);
                    //If length > 1, there could be an island.
                    if (contoursOnPlane.GetLength(0) > 0)
                    {
                        contoursTemp.Add(contoursOnPlane[0]);
                    }
                }

                //Now convert this into a double[,] array list
                if (contoursTemp.Count != 0)
                {
                    for (int i = 0; i < contoursTemp.Count; i++)
                    {
                        contours[contourCount].Add(new double[contoursTemp[i].GetLength(0), 3]);

                        for (int j = 0; j < contoursTemp[i].GetLength(0); j++)
                        {
                            VVector point = image.UserToDicom(contoursTemp[i][j], plan1);
                            contours[contourCount][i][j, 0] = (point.x);
                            contours[contourCount][i][j, 1] = (point.y);
                            contours[contourCount][i][j, 2] = (point.z);
                        }
                    }
                    contours[contourCount] = ClosedLooper(contours[contourCount]);
                }
            }
            return contours;

        }

        public static List<Structure> GetStructuresPTV(StructureSet structureSet, PlanSetup plan1, Image image)
        {

            List<Structure> ROI = new List<Structure>();    //Saving in a list because I only have read access.
            string organName = "";
            List<VVector[]> contoursTemp = new List<VVector[]>();
            foreach (Structure structure in structureSet.Structures)
            {
                organName = structure.Name;
                if (organName.ToLower().Contains("ptv"))
                {
                    ROI.Add(structure);

                }
            }
            return ROI;

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
            if (point1[2] == z)
            {
                return point1;
            }
            if (point2[2] == z)
            {
                return point2;
            }
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

        //public bool PolygonCollision(Polygon polygonA,
        //                      Polygon polygonB, Vector velocity)
        //{
        //bool result;

        //int edgeCountA = polygonA.Edges.Count;
        //int edgeCountB = polygonB.Edges.Count;
        //float minIntervalDistance = float.PositiveInfinity;
        //Vector translationAxis = new Vector();
        //Vector edge;

        //// Loop through all the edges of both polygons
        //for (int edgeIndex = 0; edgeIndex < edgeCountA + edgeCountB; edgeIndex++)
        //{
        //    if (edgeIndex < edgeCountA)
        //    {
        //        edge = polygonA.Edges[edgeIndex];
        //    }
        //    else
        //    {
        //        edge = polygonB.Edges[edgeIndex - edgeCountA];
        //    }

        //    // ===== 1. Find if the polygons are currently intersecting =====

        //    // Find the axis perpendicular to the current edge
        //    Vector axis = new Vector(-edge.Y, edge.X);
        //    axis.Normalize();

        //    // Find the projection of the polygon on the current axis
        //    float minA = 0; float minB = 0; float maxA = 0; float maxB = 0;
        //    ProjectPolygon(axis, polygonA, ref minA, ref maxA);
        //    ProjectPolygon(axis, polygonB, ref minB, ref maxB);

        //    // Check if the polygon projections are currentlty intersecting
        //    if (IntervalDistance(minA, maxA, minB, maxB) > 0)\
        //result.Intersect = false;

        //    // ===== 2. Now find if the polygons *will* intersect =====

        //    // Project the velocity on the current axis
        //    float velocityProjection = axis.DotProduct(velocity);

        //    // Get the projection of polygon A during the movement
        //    if (velocityProjection < 0)
        //    {
        //        minA += velocityProjection;
        //    }
        //    else
        //    {
        //        maxA += velocityProjection;
        //    }

        //    // Do the same test as above for the new projection
        //    float intervalDistance = IntervalDistance(minA, maxA, minB, maxB);
        //    if (intervalDistance > 0) result.WillIntersect = false;

        //    // If the polygons are not intersecting and won't intersect, exit the loop
        //    if (!result.Intersect && !result.WillIntersect) break;

        //    // Check if the current interval distance is the minimum one. If so store
        //    // the interval distance and the current distance.
        //    // This will be used to calculate the minimum translation vector
        //    intervalDistance = Math.Abs(intervalDistance);
        //    if (intervalDistance < minIntervalDistance)
        //    {
        //        minIntervalDistance = intervalDistance;
        //        translationAxis = axis;

        //        Vector d = polygonA.Center - polygonB.Center;
        //        if (d.DotProduct(translationAxis) < 0)
        //            translationAxis = -translationAxis;
        //    }
        //}

        //// The minimum translation vector
        //// can be used to push the polygons appart.
        //if (result.WillIntersect)
        //    result.MinimumTranslationVector =
        //           translationAxis * minIntervalDistance;

        //return result;
        // }
        public static T[] Populate<T>(T[] arr, T value)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = value;
            }
            return arr;
        }
        public static Tuple<bool[], double> OverlapValues(List<List<double[,]>> parotid, List<Structure> PTVs, List<Structure> ROI)
        {
            
            bool[] overlapValues = new bool[parotid.Count];
            overlapValues = Populate(overlapValues, false);
            double x, y, z; //minY, maxY, minX, maxX;
            x = 0;
            y = 0;
            z = 0;

            for (int i = 0; i < parotid.Count; i++)
            {
                for (int j = 0; j < parotid[i].Count; j++) //For each contour of each subregion
                {
                    //Polygon parPoly = new Polygon();
                    //PointCollection parPoints = new PointCollection();
                    for (int k = 0; k < parotid[i][j].GetLength(0); k++)
                    {

                        x = parotid[i][j][k, 0];
                        y = parotid[i][j][k, 1];
                        z = parotid[i][j][k, 2];
                        VVector point = new VVector(x, y, z);
                        //parPoints.Add(new Point(x, y));

                        //Determine potential subregions for the voxel:
                        for (int ptv = 0; ptv < PTVs.Count; ptv++) //go through each ptv
                        {
                            if (PTVs[ptv].IsPointInsideSegment(point))
                            {
                                overlapValues[i] = true;
                            }
                        }



                        //if (PTVs[ptv].IsPointInsideSegment()

                        //for (int ptvCont = 0; ptvCont < PTVs[ptv].Count - 1; ptvCont++) // each contour of each ptv
                        //{


                        //    if ((PTVs[ptv][ptvCont].GetLength(0) != 0) && (PTVs[ptv][ptvCont + 1].GetLength(0) != 0))
                        //    {

                        //        if ((PTVs[ptv][ptvCont][0, 2] <= z) && (PTVs[ptv][ptvCont + 1][0, 2] >= z))    //Is this point within z range of this region?
                        //        {

                        //            //is point within max bounds of the region? 
                        //            minX = Math.Min(min(PTVs[ptv][ptvCont], 0), min(PTVs[ptv][ptvCont + 1], 0));
                        //            minY = Math.Min(min(PTVs[ptv][ptvCont], 1), min(PTVs[ptv][ptvCont + 1], 1));
                        //            maxX = Math.Min(max(PTVs[ptv][ptvCont], 0), max(PTVs[ptv][ptvCont + 1], 0));
                        //            maxY = Math.Min(max(PTVs[ptv][ptvCont], 1), max(PTVs[ptv][ptvCont + 1], 1));

                        //            if ((x > minX) && (x < maxX) && (y > minY) && (y < maxY))
                        //            {
                        //                ////Now interpolate a contour between the two.
                        //                //Polygon PTVPoly = new Polygon();
                        //                //PointCollection PTVPoints = new PointCollection();
                        //                double [,] ptvSlice = InterpBetweenContours(PTVs[ptv][ptvCont], PTVs[ptv][ptvCont + 1], z);


                        //            }
                        //        }
                        //    }


                        //}
                    }

                }
            }
            //Quantify fraction of parotid overlapping:
            double overlapFrac = (1 -( ROI[1].Volume / ROI[0].Volume)) * 100;
            string output = "";
            string beginSpace = "         ";    //how much space between columns?
            string middleSpace = "     ";
            output += "Overlapping Regions:" + System.Environment.NewLine;
            output += "SubRegion:  |  Overlap:" + System.Environment.NewLine;
            output += "--------------------" + System.Environment.NewLine;

            for (int i = 0; i < overlapValues.GetLength(0); i++)
            {
                if (i == 10)
                {
                    beginSpace = "        ";
                }
                output += beginSpace + (i + 1) + middleSpace + overlapValues[i].ToString() + System.Environment.NewLine;
                output += "--------------------" + System.Environment.NewLine;
                
            }
            output += "Percent of Gland overlapping PTV: " + String.Format("{0:0.00}", overlapFrac);
            MessageBox.Show(output);


            return Tuple.Create(overlapValues, overlapFrac);
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
            double[,] c = new double[a.GetLength(0), 3];

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
        public static Polygon InterpBetweenContours(double[,] a, double[,] b, double zVal, bool makePolygon)
        //interpolate between contours a and b at the specified z value. (z is between the contours)
        {

            double x, y, z;
            double[,] c = new double[a.GetLength(0), 3];
            Polygon pol = new Polygon();
            PointCollection pc = new PointCollection();

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
                pc.Add(new Point(c[i, 0], c[i, 1]));
            }
            pol.Points = pc;
            return pol;
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











    }
}