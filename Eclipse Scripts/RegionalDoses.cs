using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;


namespace VMS.TPS
{
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
            DoseMatrix doses = new DoseMatrix();
            //Get the dose matrix dimensions:	
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
            //MessageBox.Show(doses.Matrix[39, 39, 19].ToString());
            //MessageBox.Show(dose.DoseMax3D.Unit.ToString());	


            //Now get the contours. Get the contralateral parotid as parotid with the smallest dose.
            StructureSet structureSet = context.StructureSet;
            double meanDose = 100000;     //will be updated for each parotid with smaller mean dose.
            Structure parotid;
            foreach (Structure structure in structureSet.Structures)
            {
                if ((structure.Name.ToLower().Contains("par"))&& !(structure.Name.ToLower().Contains("opt")))
                {
                    //this should be a parotid... check its mean dose, use it if its the smallest.
                    DoseValue structDose = CalculateMeanDose(plan1, structure);
                    if (structDose.Dose < meanDose)
                    {
                        parotid = structure;
                    }
                }
            }
            MessageBox.Show(structDose.Dose.ToString());


        }

        public DoseValue CalculateMeanDose(PlanSetup plan, Structure structure)
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
                        if (segmentStride[i](i))
                        {
                            if (doseProfile == null)
                            {
                                doseProfile = dose.GetDoseProfile(start, end, doseArray);
                            }

                            double doseValue = doseProfile[i](i).Value;
                            if (!Double.IsNaN(doseValue))
                            {
                                sum += doseProfile[i](i).Value;
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


    }

}