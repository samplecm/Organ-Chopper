using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dicom.Imaging.Render;
using Dicom;
using Dicom.Imaging;
using DicomChopper.DataConverting;

namespace DicomChopper.Doses
{
    public class DicomDose
    {
        public static DicomDataset LoadDoseInfo(string dosePath, string patientID)
        {
            var rawDoseFile = DicomFile.Open(dosePath);
            var doseData = rawDoseFile.Dataset;
            string doseID = doseData.GetString(DicomTag.PatientID);




            //make sure name matches in struct and dose files:
            if (doseID != patientID)
            {
                Console.WriteLine("Warning, patient ID in struct and dose file do not match. Press enter to continue");
                Console.ReadLine();
            }
            var deltaX = doseData.GetString(DicomTag.PixelSpacing);


            return doseData;
        }
        public static void MeanDoses(List<List<double[,]>> contours, string dosePath, string patientID)
        {
            //First Load the Dose file.
            DicomDataset doseData = LoadDoseInfo(dosePath, patientID);
            DoseMatrix dose = new DoseMatrix(doseData);
            Console.WriteLine(dose.Matrix[69, 99, 29]);
            Console.ReadLine();

        }
        public static double[] GetXValues(DicomDataset doseData)
        {
            int[] orientation = doseData.Get<int[]>(DicomTag.ImageOrientationPatient);
            double[] doseCorner = doseData.Get<double[]>(DicomTag.ImagePositionPatient);
            int cols = doseData.Get<int>(DicomTag.Columns);
            double[] pixelSpacing = doseData.Get<double[]>(DicomTag.PixelSpacing);
            double[] xValues = new double[cols];
            if ((orientation[3] == 0) && (orientation[4] == 1) && (orientation[5] == 0))
            {
                for (int i = 0; i < cols; i++)
                {
                    xValues[i] = pixelSpacing[0] * i + doseCorner[0];
                }
            }
            else if ((orientation[3] == 0) && (orientation[4] == -1) && (orientation[5] == 0))
            {
                for (int i = 0; i < cols; i++)
                {
                    xValues[i] = -pixelSpacing[0] * i + doseCorner[0];
                    xValues = DataConversion.ReverseArray(xValues);
                }
            }
            else
            {
                Console.WriteLine("patient orientation error encountered. Terminating.");
                Environment.Exit(2);
            }
            return xValues;
        }
        public static double[] GetYValues(DicomDataset doseData)
        {
            int[] orientation = doseData.Get<int[]>(DicomTag.ImageOrientationPatient);
            double[] doseCorner = doseData.Get<double[]>(DicomTag.ImagePositionPatient);
            int rows = doseData.Get<int>(DicomTag.Rows);
            double[] pixelSpacing = doseData.Get<double[]>(DicomTag.PixelSpacing);
            double[] yValues = new double[rows];
            if ((orientation[0] == 1) && (orientation[1] == 0) && (orientation[2] == 0))
            {
                for (int i = 0; i < rows; i++)
                {
                    yValues[i] = pixelSpacing[1] * i + doseCorner[1];
                }
            }
            else if ((orientation[0] == -1) && (orientation[1] == 0) && (orientation[2] == 0))
            {
                for (int i = 0; i < rows; i++)
                {
                    yValues[i] = -pixelSpacing[1] * i + doseCorner[1];
                    yValues = DataConversion.ReverseArray(yValues);
                }
            }
            else
            {
                Console.WriteLine("patient orientation error encountered. Terminating.");
                Environment.Exit(2);
            }
            return yValues;
        }
        public static double[] GetZValues(DicomDataset doseData)
        //This method gives back a double array with elements corresponding to the z-values for the doseMatrix 3rd dimension.
        {
            double[] offsetVector = doseData.Get<double[]>(DicomTag.GridFrameOffsetVector);
            double[] doseCorner = doseData.Get<double[]>(DicomTag.ImagePositionPatient);
            //If first element starts with a 0, its a relative offset. if it starts with non-zero, it is patient coordinate system values
            if (offsetVector[0] != 0)
            {
                for (int i = 0; i < offsetVector.Length; i++)
                {
                    offsetVector[i] -= offsetVector[0];
                }

            }
            double[] zValues = new double[offsetVector.Length];
            for (int i = 0; i < offsetVector.Length; i++)
            {
                zValues[i] = offsetVector[i] + doseCorner[2];
            }
            //Now flip if sorted by descending z. 
            if (zValues[1] - zValues[0] < 0)
            {
                zValues = DataConversion.ReverseArray(zValues);
            }
            return zValues;

        }
        public static double[,,] GetDoseMatrix(DicomDataset doseData)
        {
            var doseDims = new DicomImage(doseData);
            double[,,] doseMatrix = new double[doseDims.Height, doseDims.Width, doseDims.NumberOfFrames];
            int numFrames = doseDims.NumberOfFrames;
            double max = 0;
            //transfer the data from the byte[] array to a nice 3d dose matrix
            var scaling = doseData.Get<double>(DicomTag.DoseGridScaling);
            int[] orientation = doseData.Get<int[]>(DicomTag.ImageOrientationPatient);
            int cols = doseData.Get<int>(DicomTag.Columns);
            int rows = doseData.Get<int>(DicomTag.Rows);
            if ((orientation[3] == 0) && (orientation[4] == 1) && (orientation[5] == 0) && (orientation[0] == 1) && (orientation[1] == 0) && (orientation[2] == 0))
            {
                for (int frame = 0; frame < numFrames; frame++)
                {
                    int elem = 0; //for each element in the raw byte[] array
                    int row = 0;
                    var rawData = (GrayscalePixelDataU32)PixelDataFactory.Create(DicomPixelData.Create(doseData), frame);
                    var frameArray = rawData.Data;

                    for (int y = 0; y < doseMatrix.GetLength(0); y++)
                    {
                        for (int x = 0; x < doseMatrix.GetLength(1); x++)
                        {
                            doseMatrix[y, x, frame] = scaling * frameArray[elem];

                            if (frameArray[elem] > max)
                            {
                                max = frameArray[elem];
                            }
                            elem++;
                        }
                        row += 2;
                    }
                }
            }
            else if ((orientation[3] == 0) && (orientation[4] == -1) && (orientation[5] == 0))
            {
                for (int frame = 0; frame < numFrames; frame++)
                {
                    int elem = 0; //for each element in the raw byte[] array
                    int row = 0;
                    var rawData = (GrayscalePixelDataU32)PixelDataFactory.Create(DicomPixelData.Create(doseData), frame);
                    var frameArray = rawData.Data;

                    for (int y = 0; y < doseMatrix.GetLength(0); y++)
                    {
                        for (int x = doseMatrix.GetLength(1) - 1; x >= 0; x--)
                        {
                            doseMatrix[y, x, frame] = scaling * frameArray[elem];

                            if (frameArray[elem] > max)
                            {
                                max = frameArray[elem];
                            }
                            elem++;
                        }
                        row += 2;
                    }
                }
            }
            else if ((orientation[0] == -1) && (orientation[1] == 0) && (orientation[5] == 0))
            {
                for (int frame = 0; frame < numFrames; frame++)
                {
                    int elem = 0; //for each element in the raw byte[] array
                    int row = 0;
                    var rawData = (GrayscalePixelDataU32)PixelDataFactory.Create(DicomPixelData.Create(doseData), frame);
                    var frameArray = rawData.Data;

                    for (int y = doseMatrix.GetLength(0) - 1; y >= 0; y--)
                    {
                        for (int x = 0; x < doseMatrix.GetLength(1); x++)
                        {
                            doseMatrix[y, x, frame] = scaling * frameArray[elem];

                            if (frameArray[elem] > max)
                            {
                                max = frameArray[elem];
                            }
                            elem++;
                        }
                        row += 2;
                    }
                }
            }
            else if ((orientation[0] == -1) && (orientation[1] == 0) && (orientation[5] == 0) && (orientation[3] == 0) && (orientation[4] == -1) && (orientation[5] == 0))
            {
                for (int frame = 0; frame < numFrames; frame++)
                {
                    int elem = 0; //for each element in the raw byte[] array
                    int row = 0;
                    var rawData = (GrayscalePixelDataU32)PixelDataFactory.Create(DicomPixelData.Create(doseData), frame);
                    var frameArray = rawData.Data;

                    for (int y = doseMatrix.GetLength(0) - 1; y >= 0; y--)
                    {
                        for (int x = doseMatrix.GetLength(1) - 1; x >= 0; x--)
                        {
                            doseMatrix[y, x, frame] = scaling * frameArray[elem];

                            if (frameArray[elem] > max)
                            {
                                max = frameArray[elem];
                            }
                            elem++;
                        }
                        row += 2;
                    }
                }
            }
                //Now if x,y,z not in ascending order, flip the matrix. 
                return doseMatrix;   
        }
    }
}
