using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.IO;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
//This script exports the mean dose to all structures in the plan

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
            //Is there a structure set?
            if (context.StructureSet == null)
            {
                MessageBox.Show("Oh No!: no structure set found! :(");
            }

            Patient testPatient = context.Patient;
            PlanSetup plan1 = context.PlanSetup;
            Image image = context.Image;
            Dose dose = plan1.Dose;
            plan.DoseValuePresentation = DoseValuePresentation.Absolute;
            //Get the structures: 
            StructureSet structureSet = context.StructureSet;
            //Make A list for structure names, and a list for structure mean doses
            double[] organDoses = new double[18];
            string[] organNames = new string[18];
            organNames[0] = ("stem"); //0
            organNames[1] = ("stemPRV");//1
            organNames[2] = ("cord");//2
            organNames[3] = ("cordPRV");//3
            organNames[4] = ("PTV70");//4
            organNames[5] = ("PTV63");//5
            organNames[6] = ("PTV60");//6
            organNames[7] = ("PTV56");//7
            organNames[8] = ("PTV54");//8
            organNames[9] = ("PTV50");//9
            organNames[10] = ("PTV45");//10
            organNames[11] = ("PTV35");//11
            organNames[12] = ("RPar");//12
            organNames[13] = ("LPar");//13
            organNames[14] = ("RSub");//14
            organNames[15] = ("LSub");//15
            organNames[16] = ("OCav");//16
            organNames[17] = ("Lar");//17

            //Make the CSV: 
            DoseExporter(structureSet, organDoses, organNames, plan1, testPatient);





        }
        public static void DoseExporter(StructureSet structureSet, double[] organDoses, string[] organNames, PlanSetup plan1, Patient testPatient)
        {
            //Get the mean doses for each organ 
            foreach (Structure structure in structureSet.Structures)
            {
                string organName = structure.Name;
                var structDose = CalculateMeanDose(plan1, structure);
                double dose = structDose.Dose;

                    ;
                if ((organName.ToLower().Contains("st")) && !(organName.ToLower().Contains("prv")))
                {
                    organDoses[0] = dose;
                }
                else if ((organName.ToLower().Contains("st")) && (organName.ToLower().Contains("prv")))
                {
                    organDoses[1] = dose;
                }
                else if ((organName.ToLower().Contains("cord")) && !(organName.ToLower().Contains("prv")))
                {
                    organDoses[2] = dose;
                }
                else if ((organName.ToLower().Contains("cord")) && (organName.ToLower().Contains("prv")))
                {
                    organDoses[3] = dose;
                }
                else if ((organName.ToLower().Contains("ptv")) && (organName.ToLower().Contains("70")))
                {
                    organDoses[4] = dose;
                }
                else if ((organName.ToLower().Contains("ptv")) && (organName.ToLower().Contains("63")))
                {
                    organDoses[5] = dose;
                }
                else if ((organName.ToLower().Contains("ptv")) && (organName.ToLower().Contains("60")))
                {
                    organDoses[6] = dose;
                }
                else if ((organName.ToLower().Contains("ptv")) && (organName.ToLower().Contains("56")))
                {
                    organDoses[7] = dose;
                }
                else if ((organName.ToLower().Contains("ptv")) && (organName.ToLower().Contains("54")))
                {
                    organDoses[8] = dose;
                }
                else if ((organName.ToLower().Contains("ptv")) && (organName.ToLower().Contains("50")))
                {
                    organDoses[9] = dose;
                }
                else if ((organName.ToLower().Contains("ptv")) && (organName.ToLower().Contains("45")))
                {
                    organDoses[10] = dose;
                }
                else if ((organName.ToLower().Contains("ptv")) && (organName.ToLower().Contains("35")))
                {
                    organDoses[11] = dose;
                }
                else if ((organName.ToLower().Contains("par")) && (organName.ToLower().Contains("r")) && !(organName.ToLower().Contains("opt")))
                {
                    organDoses[12] = dose;
                }
                else if ((organName.ToLower().Contains("par")) && (organName.ToLower().Contains("l")) && !(organName.ToLower().Contains("opt")))
                {
                    organDoses[13] = dose;
                }
                else if ((organName.ToLower().Contains("subm")) && (organName.ToLower().Contains("l")) && !(organName.ToLower().Contains("opt")))
                {
                    organDoses[14] = dose;
                }
                else if ((organName.ToLower().Contains("subm")) && (organName.ToLower().Contains("l")) && !(organName.ToLower().Contains("opt")))
                {
                    organDoses[15] = dose;
                }
                else if ((organName.ToLower().Contains("cav")) && (organName.ToLower().Contains("o")))
                {
                    organDoses[16] = dose;
                }
                else if ((organName.ToLower().Contains("lar")))
                {
                    organDoses[17] = dose;
                }
            }
            //Export to a CSV
            //make CSV for meandoses
            string fileName = plan1.Id + "_organDoses.csv";
            string path = Path.Combine(@"\\phsabc.ehcnet.ca\HomeDir\HomeDir02\csample1\Profile\Desktop\Parotid Project\Base Planning Paper\meanDoses", testPatient.LastName);
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(path, fileName)))
            {
                outputFile.WriteLine("Organ mean doses (Gy):");
                

                for (int i = 0; i < organDoses.Length; i++)
                {
                    outputFile.WriteLine(organNames[i] + ", " + String.Format("{0:0.00}", organDoses[i]));
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
    }
}
