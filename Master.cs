using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dicom;
using DicomChopper.DICOMParsing;
using DicomChopper.Segmentation;
using DicomChopper.Doses;


namespace DicomChopper
{
    class Master
    {
        static void Main(string[] args)
        {
            string structPath = @"../../../ExportedPlansEclipse/spstudy_test_002/0Gy/RS.dcm";
            string dosePath = @"../../../ExportedPlansEclipse/spstudy_test_002/0Gy/RD.dcm";
            //define the number of slices desired in the x,y,z directions:
            int numCutsX = 2;
            int numCutY = 1;
            int numCutsZ = 2;
            int SSFactor = 4; //supersampling factors
            int SSFactorZ = 1;
            //First load the RT struct dicom file.
            Console.WriteLine("Reading Dicom Struct file...");
            var structFile = DicomFile.Open(structPath).Dataset;

            //Get patient ID:
            string patientID = structFile.GetString(DicomTag.PatientID);

            //Get the desired ROI, and close all contours
            List<double[,]> contoursTemp = DicomParsing.FindROI(structFile, "paro", true, true);
            string organName = DicomParsing.ROIName;

            //Chop it!
            List<List<double[,]>> contours = new List<List<double[,]>>();
            contours = Chopper.Chop(contoursTemp, numCutsX, numCutY, numCutsZ, organName);

            //Plot it!
            Console.WriteLine("Would you like to plot the chopped up ROI? (y/n)");
            string input = Console.ReadLine();
            input.ToLower();
            if ((input == "y") || (input == "yes"))
            {
                ContourPlotting.Plot(contours);
            }
            //Now load a dose file.
            DicomDose.MeanDoses(contours, dosePath, patientID, SSFactor, SSFactorZ);


            Console.ReadLine();
        }
    }
}
