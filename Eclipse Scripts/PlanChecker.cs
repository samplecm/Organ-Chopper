using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

[assembly: AssemblyVersion("1.0.0.1")]

namespace VMS.TPS
{
    public class Script
    {

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context /*, System.Windows.Window window, ScriptEnvironment environment*/)
        {
            
            if (context.Patient == null)
            {
                MessageBox.Show("Shucks: No patient selected! :(");
                return;
            }
            //Is there a structure set?
            if (context.StructureSet == null)
            {
                MessageBox.Show("Oh No!: no structure set found! :(");
            }
            //Is there a structure set?
            if (context.Course == null)
            {
                MessageBox.Show("Oh No!: no course set found! :(");
            }

            foreach (PlanSetup plan1 in context.PlansInScope)
            {
                Image image = context.Image;
                Dose dose = plan1.Dose;
                Patient testPatient = context.Patient;
                StructureSet structureSet = context.StructureSet;
                plan1.DoseValuePresentation = DoseValuePresentation.Absolute;
                //specify where to save report to
                string path = Path.Combine(@"\\phsabc.ehcnet.ca\HomeDir\HomeDir02\csample1\Profile\Desktop\Parotid Project\Base Planning\meanDoses", testPatient.LastName);
               
                //skip plans that aren't mine:
                if ((plan1.Id.ToLower().Contains("b")) || (plan1.Id.ToLower().Contains("new")) || !((plan1.Id.ToLower().Contains("main"))))
                {
                    continue;
                }
                PlanChecker(structureSet, plan1, testPatient, path);
            }
        }
        public static void PlanChecker(StructureSet ss, PlanSetup p, Patient patient, string path)
        {
            //Export to a text file
            //make txt for meandoses
            string fileName = p.Id + "_PlanCheck.txt";

            //Get the current date and time:
            var culture = new CultureInfo("en-US");
            DateTime localDate = DateTime.Now;

            using (StreamWriter outputFile = new StreamWriter(Path.Combine(path, fileName)))
            {
                outputFile.WriteLine("Treatment Plan Verification Report \n");
                outputFile.WriteLine("Date of Report: " + localDate.ToString(culture));
                outputFile.WriteLine("Patient:");
                outputFile.WriteLine("last name: " + patient.LastName.ToString());
                outputFile.WriteLine("last name: " + patient.FirstName.ToString());
                outputFile.WriteLine("\n \n");

                List<string> regionOutput = new List<string>();
                List<string> violated = new List<string>();   //hold names of violated regions

                double prescDose = p.TotalPrescribedDose.Dose;
                foreach (Structure s in ss.Structures)
                {
                    string organName = s.Name;
                    if ((organName.ToLower().Contains("st")) && !(organName.ToLower().Contains("prv")))    //Brain stem
                    {
                        bool viol = false;
                        string organOut = "---------------------\n";
                        DVHData dvh = p.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
                        double maxDose = dvh.MaxDose.Dose;
                        var a = p.GetDoseAtVolume(s, 95, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                        DoseValue fiftyGy = new DoseValue(5000, "cGy");
                        double V50 = p.GetVolumeAtDose(s, fiftyGy, VolumePresentation.Relative);
                        DoseValue fortyGy = new DoseValue(4000, "cGy");
                        double V40 = p.GetVolumeAtDose(s, fiftyGy, VolumePresentation.Relative);
                        organOut += organName + "\n\n";

                        if ((maxDose > 5000) || (V50 > 1) || (V40 > 10))
                        {
                            violated.Add(organName);
                            viol = true;
                        }
                        organOut += "Dmax: " + string.Format("{0:0.00}", maxDose) + "cGy \n";
                        organOut += "V50: " + string.Format("{0:0.00}", V50) + "% \n";
                        organOut += "V40: " + string.Format("{0:0.00}", V40) + "% \n";
                        if (viol)
                        {
                            organOut += "VIOLATED";
                        }
                        else
                        {
                            organOut += "PASSED";
                        }
                    }
                    else if ((organName.ToLower().Contains("st")) && (organName.ToLower().Contains("prv"))) //brain prv
                    {
                        bool viol = false;
                        string organOut = "---------------------\n";
                        DVHData dvh = p.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
                        double maxDose = dvh.MaxDose.Dose;
                        var a = p.GetDoseAtVolume(s, 95, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                        DoseValue fiftyGy = new DoseValue(5000, "cGy");
                        double V50 = p.GetVolumeAtDose(s, fiftyGy, VolumePresentation.Relative);
                        DoseValue fortyGy = new DoseValue(4000, "cGy");
                        double V40 = p.GetVolumeAtDose(s, fiftyGy, VolumePresentation.Relative);
                        organOut += organName + "\n\n";

                        if ((maxDose > 5000) || (V50 > 1) || (V40 > 10))
                        {
                            violated.Add(organName);
                            viol = true;
                        }
                        organOut += "Dmax: " + string.Format("{0:0.00}", maxDose) + "cGy \n";
                        organOut += "V50: " + string.Format("{0:0.00}", V50) + "% \n";
                        organOut += "V40: " + string.Format("{0:0.00}", V40) + "% \n";
                        if (viol)
                        {
                            organOut += "VIOLATED";
                        }
                        else
                        {
                            organOut += "PASSED";
                        }
                        regionOutput.Add(organOut);

                    }
                    else if ((organName.ToLower().Contains("cord")) && !(organName.ToLower().Contains("prv")))
                    {
                        bool viol = false;
                        string organOut = "---------------------\n";
                        DVHData dvh = p.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
                        double maxDose = dvh.MaxDose.Dose;
                        var a = p.GetDoseAtVolume(s, 95, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                        DoseValue fiftyGy = new DoseValue(5000, "cGy");
                        double V50 = p.GetVolumeAtDose(s, fiftyGy, VolumePresentation.Relative);
                        DoseValue fortyGy = new DoseValue(4000, "cGy");
                        double V40 = p.GetVolumeAtDose(s, fiftyGy, VolumePresentation.Relative);
                        organOut += organName + "\n\n";

                        if ((maxDose > 5000) || (V50 > 1) || (V40 > 10))
                        {
                            violated.Add(organName);
                            viol = true;
                        }
                        organOut += "Dmax: " + string.Format("{0:0.00}", maxDose) + "cGy \n";
                        organOut += "V50: " + string.Format("{0:0.00}", V50) + "% \n";
                        organOut += "V40: " + string.Format("{0:0.00}", V40) + "% \n";
                        if (viol)
                        {
                            organOut += "VIOLATED";
                        }
                        else
                        {
                            organOut += "PASSED";
                        }
                        regionOutput.Add(organOut);
                    }
                    else if ((organName.ToLower().Contains("cord")) && (organName.ToLower().Contains("prv")))
                    {
                        bool viol = false;
                        string organOut = "---------------------\n";
                        DVHData dvh = p.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
                        double maxDose = dvh.MaxDose.Dose;
                        var a = p.GetDoseAtVolume(s, 95, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                        DoseValue fiftyGy = new DoseValue(5000, "cGy");
                        double V50 = p.GetVolumeAtDose(s, fiftyGy, VolumePresentation.Relative);
                        DoseValue fortyGy = new DoseValue(4000, "cGy");
                        double V40 = p.GetVolumeAtDose(s, fiftyGy, VolumePresentation.Relative);
                        organOut += organName + "\n\n";

                        if ((maxDose > 5000) || (V50 > 1) || (V40 > 10))
                        {
                            violated.Add(organName);
                            viol = true;
                        }
                        organOut += "Dmax: " + string.Format("{0:0.00}", maxDose) + "cGy \n";
                        organOut += "V50: " + string.Format("{0:0.00}", V50) + "% \n";
                        organOut += "V40: " + string.Format("{0:0.00}", V40) + "% \n";
                        if (viol)
                        {
                            organOut += "VIOLATED";
                        }
                        else
                        {
                            organOut += "PASSED";
                        }
                        regionOutput.Add(organOut);
                    }
                    else if ((organName.ToLower().Contains("ptv")))
                    {
                        //Get what type of PTV:
                        string typetemp = "";
                        for (int n = 0; n < organName.Length; n++)
                        {
                            if (Char.IsDigit(organName[n]))
                                typetemp += organName[n];
                        }
                        double type = Convert.ToDouble(typetemp + "00");
                        bool viol = false;
                        string organOut = "---------------------\n";
                        DVHData dvh = p.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
                        double maxDose = dvh.MaxDose.Dose;
                        DoseValue v98 = new DoseValue(type * 0.95, "cGy");
                        double V98 = p.GetVolumeAtDose(s, v98, VolumePresentation.Relative);

                        organOut += organName + "\n\n";

                        if ((maxDose > prescDose * 1.1) || (V98 < 0.98 ))
                        {
                            violated.Add(organName);
                            viol = true;
                        }
                        organOut += "Dmax: " + string.Format("{0:0.00}", maxDose) + "cGy \n";
                        organOut += "V98: " + string.Format("{0:0.00}", V98) + "% \n";
                        organOut += "Dmean: " + string.Format("{0:0.00}", dvh.MeanDose.Dose) + "cGy \n";

                        if (viol)
                        {
                            organOut += "VIOLATED";
                        }
                        else
                        {
                            organOut += "PASSED";
                        }
                        regionOutput.Add(organOut);
                    }                   
                    else if ((organName.ToLower().Contains("par")) && !(organName.ToLower().Contains("opt")) && !(organName.ToLower().Contains("l")))
                    {//Right parotid

                        bool viol = false;
                        string organOut = "---------------------\n";

                        DVHData dvh = p.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
                        double maxDose = dvh.MaxDose.Dose;
                        double meanDose = dvh.MeanDose.Dose;
                        DoseValue v20 = new DoseValue(2000, "cGy");
                        double V20 = p.GetVolumeAtDose(s, v20, VolumePresentation.Relative);
                        organOut += organName + "\n\n";
                        
                        if ((meanDose > 2000))
                        {
                            violated.Add(organName);
                            viol = true;
                        }
                        
                        organOut += "Dmean: " + string.Format("{0:0.00}", meanDose) + "cGy \n";
                        organOut += "V_20Gy: " + string.Format("{0:0.00}", V20) + "% \n";
                        organOut += "Dmax: " + string.Format("{0:0.00}", maxDose) + "cGy \n";

                        if (viol)
                        {
                            organOut += "VIOLATED";
                        }
                        else
                        {
                            organOut += "PASSED";
                        }
                        regionOutput.Add(organOut);
                    }
                    else if ((organName.ToLower().Contains("par")) && (organName.ToLower().Contains("l")) && !(organName.ToLower().Contains("opt")))
                    { //Left parotid
                        bool viol = false;
                        string organOut = "---------------------\n";

                        DVHData dvh = p.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
                        double maxDose = dvh.MaxDose.Dose;
                        double meanDose = dvh.MeanDose.Dose;
                        DoseValue v20 = new DoseValue(2000, "cGy");
                        double V20 = p.GetVolumeAtDose(s, v20, VolumePresentation.Relative);
                        organOut += organName + "\n\n";

                        if ((meanDose > 2000))
                        {
                            violated.Add(organName);
                            viol = true;
                        }

                        organOut += "Dmean: " + string.Format("{0:0.00}", meanDose) + "cGy \n";
                        organOut += "V_20Gy: " + string.Format("{0:0.00}", V20) + "% \n";
                        organOut += "Dmax: " + string.Format("{0:0.00}", maxDose) + "cGy \n";

                        if (viol)
                        {
                            organOut += "VIOLATED";
                        }
                        else
                        {
                            organOut += "PASSED";
                        }
                        regionOutput.Add(organOut);
                    }
                    else if ((organName.ToLower().Contains("par")) && (organName.ToLower().Contains("opt")) && !(organName.ToLower().Contains("l")))
                    {//Right opti parotid

                        bool viol = false;
                        string organOut = "---------------------\n";

                        DVHData dvh = p.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
                        double maxDose = dvh.MaxDose.Dose;
                        double meanDose = dvh.MeanDose.Dose;
                        DoseValue v20 = new DoseValue(2000, "cGy");
                        double V20 = p.GetVolumeAtDose(s, v20, VolumePresentation.Relative);
                        organOut += organName + "\n\n";

                        if ((meanDose > 2000))
                        {
                            violated.Add(organName);
                            viol = true;
                        }

                        organOut += "Dmean: " + string.Format("{0:0.00}", meanDose) + "cGy \n";
                        organOut += "V_20Gy: " + string.Format("{0:0.00}", V20) + "% \n";
                        organOut += "Dmax: " + string.Format("{0:0.00}", maxDose) + "cGy \n";

                        if (viol)
                        {
                            organOut += "VIOLATED";
                        }
                        else
                        {
                            organOut += "PASSED";
                        }
                        regionOutput.Add(organOut);
                    }
                    else if ((organName.ToLower().Contains("par")) && (organName.ToLower().Contains("opt")))
                    { //left opti parotid
                        bool viol = false;
                        string organOut = "---------------------\n";

                        DVHData dvh = p.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
                        double maxDose = dvh.MaxDose.Dose;
                        double meanDose = dvh.MeanDose.Dose;
                        DoseValue v20 = new DoseValue(2000, "cGy");
                        double V20 = p.GetVolumeAtDose(s, v20, VolumePresentation.Relative);
                        organOut += organName + "\n\n";

                        if ((meanDose > 2000))
                        {
                            violated.Add(organName);
                            viol = true;
                        }

                        organOut += "Dmean: " + string.Format("{0:0.00}", meanDose) + "cGy \n";
                        organOut += "V_20Gy: " + string.Format("{0:0.00}", V20) + "% \n";
                        organOut += "Dmax: " + string.Format("{0:0.00}", maxDose) + "cGy \n";

                        if (viol)
                        {
                            organOut += "VIOLATED";
                        }
                        else
                        {
                            organOut += "PASSED";
                        }
                        regionOutput.Add(organOut);
                    }
                    else if ((organName.ToLower().Contains("subm")) && (organName.ToLower().Contains("r")) && !(organName.ToLower().Contains("opt")))
                    {
                        bool viol = false;
                        string organOut = "---------------------\n";

                        DVHData dvh = p.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
                        double maxDose = dvh.MaxDose.Dose;
                        double meanDose = dvh.MeanDose.Dose;
                        DoseValue v20 = new DoseValue(2000, "cGy");
                        double V20 = p.GetVolumeAtDose(s, v20, VolumePresentation.Relative);
                        organOut += organName + "\n\n";

                        if ((meanDose > 2000))
                        {
                            violated.Add(organName);
                            viol = true;
                        }

                        organOut += "Dmean: " + string.Format("{0:0.00}", meanDose) + "cGy \n";
                        organOut += "V_20Gy: " + string.Format("{0:0.00}", V20) + "% \n";
                        organOut += "Dmax: " + string.Format("{0:0.00}", maxDose) + "cGy \n";

                        if (viol)
                        {
                            organOut += "VIOLATED";
                        }
                        else
                        {
                            organOut += "PASSED";
                        }
                        regionOutput.Add(organOut);
                    }
                    else if ((organName.ToLower().Contains("subm")) && (organName.ToLower().Contains("l")) && !(organName.ToLower().Contains("opt")))
                    {
                        bool viol = false;
                        string organOut = "---------------------\n";

                        DVHData dvh = p.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
                        double maxDose = dvh.MaxDose.Dose;
                        double meanDose = dvh.MeanDose.Dose;
                        DoseValue v20 = new DoseValue(2000, "cGy");
                        double V20 = p.GetVolumeAtDose(s, v20, VolumePresentation.Relative);
                        organOut += organName + "\n\n";

                        if ((meanDose > 2000))
                        {
                            violated.Add(organName);
                            viol = true;
                        }

                        organOut += "Dmean: " + string.Format("{0:0.00}", meanDose) + "cGy \n";
                        organOut += "V_20Gy: " + string.Format("{0:0.00}", V20) + "% \n";
                        organOut += "Dmax: " + string.Format("{0:0.00}", maxDose) + "cGy \n";

                        if (viol)
                        {
                            organOut += "VIOLATED";
                        }
                        else
                        {
                            organOut += "PASSED";
                        }
                        regionOutput.Add(organOut);
                    }
                    else if ((organName.ToLower().Contains("cav")) && (organName.ToLower().Contains("o")))
                    { //oral cavity
                        bool viol = false;
                        string organOut = "---------------------\n";

                        DVHData dvh = p.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
                        double maxDose = dvh.MaxDose.Dose;
                        double meanDose = dvh.MeanDose.Dose;
                        DoseValue v50 = new DoseValue(5000, "cGy");
                        double V50 = p.GetVolumeAtDose(s, v50, VolumePresentation.Relative);
                        organOut += organName + "\n\n";

                        if ((meanDose > 5000))
                        {
                            violated.Add(organName);
                            viol = true;
                        }

                        organOut += "Dmean: " + string.Format("{0:0.00}", meanDose) + "cGy \n";
                        organOut += "V_50Gy: " + string.Format("{0:0.00}", V50) + "% \n";
                        organOut += "Dmax: " + string.Format("{0:0.00}", maxDose) + "cGy \n";

                        if (viol)
                        {
                            organOut += "VIOLATED";
                        }
                        else
                        {
                            organOut += "PASSED";
                        }
                        regionOutput.Add(organOut);
                    }
                    else if ((organName.ToLower().Contains("lar")))
                    { //laryngopharynx
                        bool viol = false;
                        string organOut = "---------------------\n";

                        DVHData dvh = p.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
                        double maxDose = dvh.MaxDose.Dose;
                        double meanDose = dvh.MeanDose.Dose;
                        DoseValue v45 = new DoseValue(4500, "cGy");
                        double V45 = p.GetVolumeAtDose(s, v45, VolumePresentation.Relative);
                        organOut += organName + "\n\n";

                        if ((meanDose > 4500))
                        {
                            violated.Add(organName);
                            viol = true;
                        }

                        organOut += "Dmean: " + string.Format("{0:0.00}", meanDose) + "cGy \n";
                        organOut += "V_45Gy: " + string.Format("{0:0.00}", V45) + "% \n";
                        organOut += "Dmax: " + string.Format("{0:0.00}", maxDose) + "cGy \n";

                        if (viol)
                        {
                            organOut += "VIOLATED";
                        }
                        else
                        {
                            organOut += "PASSED";
                        }
                        regionOutput.Add(organOut);
                    }
                }
                outputFile.WriteLine("Violated Structures: ");
                outputFile.WriteLine("---------------------");
                for (int i = 0; i < violated.Count; i++)
                {
                    outputFile.WriteLine(violated[i]);
                }
                outputFile.WriteLine("---------------------");
                for (int i = 0; i < regionOutput.Count; i++)
                {
                    outputFile.WriteLine(regionOutput[i]);                   
                }

            }
        }
    }



}
