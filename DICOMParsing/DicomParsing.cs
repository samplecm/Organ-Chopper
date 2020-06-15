using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dicom;
using DicomChopper.Segmentation;
using DicomChopper.DataConverting;
using NumSharp;

namespace DicomChopper.DICOMParsing
{
    class DicomParsing
    //contains method FindROI for saving the desired ROI contours, and attribute ROIName that is the name
    //of the ROI selected (saved after FindROI is called).
    {
        public static string ROIName;

        //Method for finding the index of the parotid in the list of organs.
        public static List<double[,]> FindROI(DicomDataset structFile, string containsName) //Change from void after!!!
        {
            //First make an array that will hold potential indices. Make it size 10 for redundancy
            int[] indices = new int[10];
            //Now make a dictionary to hold all potential organ names, and their index in the structFile.
            Dictionary<string, int> organList = new Dictionary<string, int>();


            //Now loop through all organs to check for matches and add to dictionary organList.
            var structureSet = structFile.GetSequence(DicomTag.StructureSetROISequence).Items;

            for (int count = 0; count < structureSet.Count; count++)
            {
                string organName = structureSet[count].GetString(DicomTag.ROIName);
                if (organName.ToLower().Contains(containsName))
                {
                    organList.Add(organName, count);
                }

            }

            //Now ask the user which one they want.
            Console.WriteLine("Please Select the desired ROI: \n");
            Dictionary<string, int>.KeyCollection keys = organList.Keys; //Get the keys from the dictionary

            int i = 1;
            foreach (string key in keys)//write all options to the console
            {
                Console.WriteLine(i + ": {0}", key);
                i++;
            }
            Console.Write("\n Enter a number: ");
            string input = Console.ReadLine();
            int inputNum;
            while ((!Int32.TryParse(input, out inputNum)) || (inputNum > (i - 1)))
            {
                Console.WriteLine("Error: enter only the corresponding integer for the desired ROI.");
                Console.Write("Enter a number: ");
                input = Console.ReadLine();
            }

            //subtract 1 from inputNum to get correct index.
            inputNum--;
            ROIName = organList.ElementAt(inputNum).Key;

            //Now the dictionary value for the key organSelection gives the correct ROI.
            int organIndex = organList[ROIName];
            var rawContours = structFile.GetSequence(DicomTag.ROIContourSequence).Items[organIndex].GetSequence(DicomTag.ContourSequence).Items; //This is the list of all 2d Contours for the organ.

            
            double[] tempContours;
            double[,] finalContours;
            //Make a list which will hold the different contours.
            List<double[,]> contours = new List<double[,]>();
            //each element is an array holding the contour data for each contour of the specified organ
            for (i = 0; i < rawContours.Count; i++)
            {
                //initialize size of array, then load in contour data.
                tempContours = rawContours[i].Get<double[]>(DicomTag.ContourData);

                //Now convert from 1 column to 3 for x,y,z:
                int row = 0;
                finalContours = new double[tempContours.Length / 3, 3];
                for (int j = 0; j < tempContours.Length; j++)
                {
                    int column = j % 3;

                    finalContours[row, column] = tempContours[j];

                    if (column == 2)
                    {
                        row++;
                    }

                }
                contours.Add(finalContours);

            }
            Console.WriteLine("Successfully retrieved contours.");
            return contours;


            }

        public static List<double[,]> FindROI(Dicom.DicomDataset structFile, string containsName, bool closeContours, bool removeIslands) //Change from void after!!!
        {
            //First make an array that will hold potential indices. Make it size 10 for redundancy
            int[] indices = new int[10];
            //Now make a dictionary to hold all potential organ names, and their index in the structFile.
            Dictionary<string, int> organList = new Dictionary<string, int>();


            //Now loop through all organs to check for matches and add to dictionary organList.
            var structureSet = structFile.GetSequence(DicomTag.StructureSetROISequence).Items;

            for (int count = 0; count < structureSet.Count; count++)
            {
                string organName = structureSet[count].GetString(DicomTag.ROIName);
                if (organName.ToLower().Contains(containsName))
                {
                    organList.Add(organName, count);
                }

            }

            //Now ask the user which one they want.
            Console.WriteLine("Please Select the desired ROI: \n");
            Dictionary<string, int>.KeyCollection keys = organList.Keys; //Get the keys from the dictionary

            int i = 1;
            foreach (string key in keys)//write all options to the console
            {
                Console.WriteLine(i + ": {0}", key);
                i++;
            }
            Console.Write("\n Enter a number: ");
            string input = Console.ReadLine();
            int inputNum;
            while ((!Int32.TryParse(input, out inputNum)) || (inputNum > (i - 1)))
            {
                Console.WriteLine("Error: enter only the corresponding integer for the desired ROI.");
                Console.Write("Enter a number: ");
                input = Console.ReadLine();
            }

            //subtract 1 from inputNum to get correct index.
            inputNum--;
            ROIName = organList.ElementAt(inputNum).Key;

            //Now the dictionary value for the key organSelection gives the correct ROI.
            int organIndex = organList[ROIName];
            var rawContours = structFile.GetSequence(DicomTag.ROIContourSequence).Items[organIndex].GetSequence(DicomTag.ContourSequence).Items; //This is the list of all 2d Contours for the organ.

            //make a jagged array to hold all contours
            //double[][] tempContours = new double[rawContours.Count][];
            double[,] finalContours;
            //Make a list which will hold the different contours.
            List<double[,]> contours = new List<double[,]>();
            //each element is an array holding the contour data for each contour of the specified organ
            for (i = 0; i < rawContours.Count; i++)
            {
                //initialize size of array, then load in contour data.
                double[] tempContours = rawContours[i].Get<double[]>(DicomTag.ContourData);

                //Now convert from 1 column to 3 for x,y,z:
                int row = 0;
                finalContours = new double[tempContours.Length / 3, 3];
                for (int j = 0; j < tempContours.Length; j++)
                {
                    int column = j % 3;

                    finalContours[row, column] = tempContours[j];

                    if (column == 2)
                    {
                        row++;
                    }

                }
                contours.Add(finalContours);

            }
            if(closeContours == true)    //close contours if true
            {
                contours = ContourFixing.ClosedLooper(contours);
            }
            //if (removeIslands == true)    //remove islands if true
            //{
            //    contours = ContourFixing.IslandRemover(contours);
            //}
            Console.WriteLine("Successfully retrieved contours.");

            //convert back to a double array.
            return contours;


        }

    }

    }
