using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;


namespace VMS.TPS
{

    public class Script
    {
        public void Execute(ScriptContext context)
        {
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
            //DoseMatrix doses = GetDoseMatrix(dose, image, plan1);
            //Get the dose matrix dimensions:	

            //MessageBox.Show(doses.Matrix[39, 39, 19].ToString());
            //MessageBox.Show(dose.DoseMax3D.Unit.ToString());	


            //Now get the contours. Get the contralateral parotid as parotid with the smallest dose.
            StructureSet structureSet = context.StructureSet;
            var tuple = GetContours(structureSet, plan1);    //returns contours and organ name.
            List<double[,]> contours = tuple.Item1;
            string organName = tuple.Item2;
            MessageBox.Show(contours[0][0, 2].ToString());
            //List<List<double[,]>> choppedContours = Chop(contoursTemp, numCutsX, numCutY, numCutsZ, organName);


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
                xValues[i] = (dose.Origin + dose.XDirection * i).x;
            }
            for (int i = 0; i < ySize; i++)
            {
                yValues[i] = (dose.Origin + dose.YDirection * i).y;
            }
            for (int i = 0; i < zSize; i++)
            {
                zValues[i] = (dose.Origin + dose.ZDirection * i).z;
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

        public static Tuple<List<double[,]>, string> GetContours(StructureSet structureSet, PlanSetup plan1)
        {
            double meanDose = 100000;     //will be updated for each parotid with smaller mean dose.
            List<Structure> ROI = new List<Structure>();    //Saving in a list because I only have read access.
            DoseValue structDose;
            int count = 0;
            string organName;
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
                    int keeper = 0;
                    int numPoints = 0;
                    for (int cont = 0; cont < contoursOnPlane.GetLength(0); cont++)
                    {
                        if (contoursOnPlane[cont].GetLength(0) > numPoints)
                        {
                            keeper = cont;
                        }
                    }
                    contoursTemp.Add(contoursOnPlane[keeper]);
                }
            }
            //MessageBox.Show(contoursTemp[0][0].z.ToString());
            //Now convert this into a double[,] array list
            List<double[,]> contours = new List<double[,]>();
            for (int i = 0; i < contoursTemp.Count; i++)
            {
                contours.Add(new double[contoursTemp[i].GetLength(0), 3]);
                for (int j = 0; j < contoursTemp[i].GetLength(0) - 1; j++)
                {
                    contours[i][j, 0] = contoursTemp[i][j].x;
                    contours[i][j, 1] = contoursTemp[i][j].y;
                    contours[i][j, 2] = contoursTemp[i][j].z;
                }
            }
            return Tuple.Create(contours, organName);
        }
    }

}