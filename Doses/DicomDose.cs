using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dicom;
using Dicom.Imaging;
namespace DicomChopper.Doses
{
    public class DicomDose
    {
        public static void LoadDoseDicom(string dosePath, string patientID)
        {
            var rawDoseFile = DicomFile.Open(dosePath);
            var doseData = rawDoseFile.Dataset;
            string doseID = doseData.GetString(DicomTag.PatientID);
            var deltaX = doseData.GetString(DicomTag.PixelSpacing);

            //make sure name matches in struct and dose files:
            if (doseID != patientID)
            {
                Console.WriteLine("Warning, patient ID in struct and dose file do not match. Press enter to continue");
                Console.ReadLine();
            }
            var doseCorner = doseData.Get<string[]>(DicomTag.ImagePositionPatient);

            DicomPixelData pixelData = DicomPixelData.Create(doseData);
            var x = pixelData.GetFrame(1);
            Console.Read();
        }
        public static void MeanDoses(List<List<double[,]>> contours, string dosePath, string patientID)
        {
            //First Load the Dose file.
            LoadDoseDicom(dosePath, patientID);
            

        }

        //public static double[] NumbersFromString()
        //{

        //}
    }
}
