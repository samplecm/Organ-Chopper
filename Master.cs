using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dicom;
using DicomChopper.DICOMParsing;
using DicomChopper.Segmentation;
using DicomChopper.DataConverting;
using NumSharp;
using MatLib;



namespace DicomChopper
{
    class Master
    {
        static void Main(string[] args)
        {
            //define the number of slices desired in the x,y,z directions:
            int numCutsX = 2;
            int numCutY = 1;
            int numCutsZ = 2;
            //First load the RT struct dicom file.
            //
            string structPath = @"../../ExportedPlansEclipse/spstudy_test_002/0Gy/RS.dcm";
            Console.WriteLine("Reading Dicom Struct file...");
            var structFile = DicomFile.Open(structPath).Dataset;

            //Get patient ID:
            //
            string patientID = structFile.GetString(DicomTag.PatientID);

            var organList = structFile.GetSequence(DicomTag.ROIContourSequence).Items; //Gets to list of organs

            //Get the desired ROI, and close all contours
            //
            List<double[,]> contoursTemp = DicomParsing.FindROI(structFile, "paro", true,true);
            string organName = DicomParsing.ROIName;

            //Generate txt file of contours and gnuplot it.
            //
            
            List<List<double[,]>> contours = new List<List<double[,]>>();    //Chop it up
            contours = Chopper.Chop(contoursTemp, numCutsX, numCutY, numCutsZ);

            ContourTxtMaker.ContourPlotter(contours);

            //ContourTxtMaker.ContourPlotter(contours[0]);





            Console.ReadLine();
        }
    }
}
