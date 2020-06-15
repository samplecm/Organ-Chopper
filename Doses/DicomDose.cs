using System;
using System.Collections.Generic;
using System.IO;
using Dicom.Imaging.Render;
using Dicom;
using Dicom.Imaging;
using DicomChopper.DataConverting;
using DicomChopper.Segmentation;
using DicomChopper.Geom;

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
        public static void MeanDoses(List<List<double[,]>> contours, string dosePath, string patientID, int SSFactor, int SSFactorZ)
        {
            //First Load the Dose file.
            DicomDataset doseData = LoadDoseInfo(dosePath, patientID);
            DoseMatrix dose = new DoseMatrix(doseData);
            //Get the range of the dose Matrix that surrounds the organ of interest.
            double[,,] organDoseBounds = OrganDoseBounds(contours, dose);
            DoseMatrix doseSS = DoseMatrixSuperSampler(dose, organDoseBounds, SSFactor, SSFactorZ);
            Console.WriteLine(dose.Matrix[39, 39, 9]);
            //Get file name
            Console.WriteLine("Please enter a name for the Mean Dose file.");
            string path = Directory.GetCurrentDirectory() + @"\..\..\MeanDoses";
            string nameInput = Console.ReadLine() + ".txt";

            //Find mean doses:
            double[,] meanDoses = new double[contours.Count + 1, 2];    //second column for # of dose voxels in region. Final row for whole mean
            double x, y, z, minY, maxY, minX, maxX;
            double[,] polygon;
            double[] point = new double[3]; //the interpolated contour of the dose voxel z position.
            int numIn = 0;
            for (int i = 0; i < doseSS.Matrix.GetLength(0); i++)
            {
                for (int j = 0; j < doseSS.Matrix.GetLength(1); j++)
                {
                    for (int k = 0; k < doseSS.Matrix.GetLength(2); k++)
                    {
                        //get coordinates for the dose voxel
                        x = doseSS.xValues[j];
                        y = doseSS.yValues[i];
                        z = doseSS.zValues[k];
                        
                        //Determine potential subregions for the voxel:
                        for (int region = 0; region < contours.Count; region++)
                        {
                            for (int cont = 0; cont < contours[region].Count - 1; cont++)
                            {
                                if ((contours[region][cont][0, 2] <= z)&&(contours[region][cont+1][0, 2] >= z))    //Is this point within z range of this region?
                                {
                                    //is point within max bounds of the region? 
                                    for (int idx = cont; idx <= cont + 1; idx++)    //for both these contours that z is between 
                                    {
                                        minX = Stats.min(contours[region][idx], 0);
                                        minY = Stats.min(contours[region][idx], 1);
                                        maxX = Stats.max(contours[region][idx], 0);
                                        maxY = Stats.max(contours[region][idx], 1);
                                        if ((x > minX) && (x < maxX) && (y > minY) && (y < maxY))
                                        {
                                            //Now interpolate a contour between the two.
                                            polygon = Geometry.InterpBetweenContours(contours[region][cont], contours[region][cont + 1], z);
                                            //Now point in polygon.
                                            point[0] = x;
                                            point[1] = y;
                                            point[2] = z;
                                            if (Geometry.PointInPolygon(polygon, point)){
                                                meanDoses[region, 0] += doseSS.Matrix[i, j, k];
                                                meanDoses[region, 1]++;
                                                numIn++;
                                            }
                                        }
                                    }
                                }
                            }
                        }


                    }
                }
            }
            double wholeMean = 0;
            int totalPoints = 0;
            for (int i = 0; i < meanDoses.GetLength(0); i++)
            {
                wholeMean += meanDoses[i,0];
                meanDoses[i, 0] /= meanDoses[i, 1];
                totalPoints += (int)meanDoses[i, 1];
            }
            wholeMean /= totalPoints;
            meanDoses[meanDoses.GetLength(0)-1, 0] = wholeMean;
            meanDoses[meanDoses.GetLength(0)-1, 1] = totalPoints;




            using (StreamWriter outputFile = new StreamWriter(Path.Combine(path, nameInput)))
            {
                string beginSpace = "         ";    //how much space between columns?
                string middleSpace = "     ";
                outputFile.WriteLine("Regional mean doses (Gy):");
                outputFile.WriteLine(Environment.NewLine);
                outputFile.WriteLine("SubRegion:  |  Dose:");
                outputFile.WriteLine("--------------------");

                for (int i = 0; i < meanDoses.GetLength(0)-1; i++)
                {
                    if (i == 10)
                    {
                        beginSpace = "        "; 
                    }
                    outputFile.WriteLine(beginSpace + (i+1) + middleSpace + String.Format("{0:0.00}", meanDoses[i,0]));
                    outputFile.WriteLine(Environment.NewLine);
                    outputFile.WriteLine("--------------------");
                }

                outputFile.WriteLine("Whole Mean Dose:    " + String.Format("{0:0.00}", meanDoses[meanDoses.GetLength(0)-1, 0]));
            }

        }
        public static DoseMatrix DoseMatrixSuperSampler(DoseMatrix dose, double[,,] organDoseBounds, int SSFactor, int SSFactorZ)
        {
            //First get the number of matrix elements in each direction encompassing the organ
            int xRange = (int)(organDoseBounds[0, 1, 1] - organDoseBounds[0, 1, 0] + 1);
            int yRange = (int)(organDoseBounds[1, 1, 1] - organDoseBounds[1, 1, 0] + 1);
            int zRange = (int)(organDoseBounds[2, 1, 1] - organDoseBounds[2, 1, 0] + 1);
            //Now get the z,y,z values for the supersampled matrix.
            double[] xValues = new double[xRange * SSFactor];
            double[] yValues = new double[yRange * SSFactor];
            double[] zValues = new double[zRange * SSFactorZ];
            //Get the distance between dose pixels
            double deltaX = dose.xValues[2] - dose.xValues[1];
            double deltaY = dose.yValues[2] - dose.yValues[1];
            double deltaZ = dose.zValues[2] - dose.zValues[1];
            for (int i = 0; i < xRange * SSFactor; i++)
            {
                xValues[i] = dose.xValues[(int)organDoseBounds[0, 1, 0]] + (i) * deltaX / SSFactor;
            }
            for (int i = 0; i < yRange * SSFactor; i++)
            {
                yValues[i] = dose.yValues[(int)organDoseBounds[1, 1, 0]] + (i) * deltaY / SSFactor;
            }
            for (int i = 0; i < zRange * SSFactorZ; i++)
            {
                zValues[i] = dose.zValues[(int)organDoseBounds[2, 1, 0]] + (i) * deltaZ / SSFactorZ;
            }
            //Now make a new dose array
            double[,,] doseSS = new double[yValues.Length, xValues.Length, zValues.Length];
            double[,,] test = new double[yRange, xRange, zRange];
            //First get the original values in
            for (int i = 0; i < xRange; i++)
            {
                for (int j = 0; j < yRange; j++)
                {
                    for (int k = 0; k < zRange; k++)
                    {
                        doseSS[j * SSFactor, i * SSFactor, k * SSFactorZ] = 
                            dose.Matrix[j + (int)organDoseBounds[1, 1, 0], i + (int)organDoseBounds[0, 1, 0], k+ (int)organDoseBounds[2, 1, 0]];
                        test[j,i,k] = dose.Matrix[j + (int)organDoseBounds[1, 1, 0], i + (int)organDoseBounds[0, 1, 0], k + (int)organDoseBounds[2, 1, 0]];
                    }
                }
            }

            if (SSFactor != 1)
            {
                //Now supersample along x:
                for (int k = 0; k < zRange; k++)
                {
                    for (int j = 0; j < yRange; j++)
                    {
                        for (int i = 0; i < xRange - 1; i++)
                        {
                            for (int insert = 1; insert <= SSFactor - 1; insert++)
                            {
                                doseSS[j * SSFactor, i * SSFactor + insert, k * SSFactorZ] = 
                                    doseSS[j * SSFactor, i * SSFactor, SSFactorZ * k] + ((double)insert / SSFactor) * 
                                    (doseSS[j * SSFactor, SSFactor * (i+1), k * SSFactorZ] - doseSS[j * SSFactor, i * SSFactor, k * SSFactorZ]);
                            }
                        }
                    }
                }
                //Now supersample along y:
                for (int k = 0; k < zRange; k++)
                {
                    for (int i = 0; i < xRange*SSFactor; i++)
                    {
                        for (int j = 0; j < yRange - 1; j++)
                        {
                            for (int insert = 1; insert <= SSFactor - 1; insert++)
                            {
                                doseSS[j * SSFactor + insert, i, k * SSFactorZ] = doseSS[j * SSFactor, i, k * SSFactorZ] + ((double)insert / SSFactor) * 
                                    (doseSS[(j+1) * SSFactor, i, k * SSFactorZ] - doseSS[j * SSFactor, i, k * SSFactorZ]);
                            }
                        }
                    }
                }
                if (SSFactorZ != 1)
                {
                    //Now supersample along Z:
                    for (int j = 0; j < yRange * SSFactor; j++)
                    {
                        for (int i = 0; i < xRange * SSFactor; i++)
                        {
                            for (int k = 0; k < zRange - 1; k++)
                            {
                                for (int insert = 1; insert <= SSFactorZ - 1; insert++)
                                {
                                    doseSS[j, i, k * SSFactorZ + insert] = doseSS[j, i, k * SSFactorZ] + ((double)insert / SSFactorZ) * 
                                        (doseSS[j, i, SSFactorZ * (k+1)] - doseSS[j, i, SSFactorZ * k]);
                                }
                            }
                        }
                    }
                }
            }

            //Now need to make a new dose matrix object with this data.
            DoseMatrix newMatrix = new DoseMatrix();
            newMatrix.Matrix = doseSS;
            newMatrix.xValues = xValues;
            newMatrix.yValues = yValues;
            newMatrix.zValues = zValues;
            return newMatrix;
        }

        public static double[,,] OrganDoseBounds(List<List<double[,]>> contours, DoseMatrix dose)
            //this gives a 2D array, with the first layer, first column giving the min x,y,z bounds of the organs
            //first layer second column gives the min x,y,z indices in the dose matrix,
            //second layer is the same except for the max. 
        {
            int col = 0;
            int row = 0;
            double[,,] organDoseBounds = new double[3, 2, 2];
            //Set inital values which will be replaced by max, mins.
            for (int i = 0; i < 6; i++)
            {
                if (i == 3)
                {
                    col++;
                    row = 0;
                }
                organDoseBounds[row, col, 0] = 1000;
                row ++;
            }
            col = 0;
            row = 0;
            for (int i = 0; i < 6; i++)
            {
                if (i == 3)
                {
                    col++;
                    row = 0;
                }
                organDoseBounds[row, col, 1] = -1000;
                row++;
            }

            for (int i = 0; i < contours.Count; i++)
            {
                for (int j = 0; j < contours[i].Count; j++)
                {
                    for (int k = 0; k < contours[i][j].Length /3; k++)
                    {
                        //get min bounds
                        if (contours[i][j][k, 0] < organDoseBounds[0, 0, 0])
                        {
                            organDoseBounds[0, 0, 0] = contours[i][j][k, 0];
                        }else if (contours[i][j][k, 1] < organDoseBounds[1, 0, 0])
                        {
                            organDoseBounds[1, 0, 0] = contours[i][j][k, 1];
                        }
                        else if (contours[i][j][k, 2] < organDoseBounds[2, 0, 0])
                        {
                            organDoseBounds[2, 0, 0] = contours[i][j][k, 2];
                        }
                        //Get max bounds
                        if (contours[i][j][k, 0] > organDoseBounds[0, 0, 1])
                        {
                            organDoseBounds[0, 0, 1] = contours[i][j][k, 0];
                        }
                        else if (contours[i][j][k, 1] > organDoseBounds[1, 0, 1])
                        {
                            organDoseBounds[1, 0, 1] = contours[i][j][k, 1];
                        }
                        else if (contours[i][j][k, 2] > organDoseBounds[2, 0, 1])
                        {
                            organDoseBounds[2, 0, 1] = contours[i][j][k, 2];
                        }
                    }
                }
            }
            //Now find what indices in the dose files encapsulate these bounds.
            //Do this by finding min value that is between -2.5 and 0mm below minimum,
            //And max value between 0 and 2.5mm above max.
            for (int x = 0; x < dose.xValues.Length; x++)
            {
                if ((organDoseBounds[0,0,0] - dose.xValues[x] > 0)&&(organDoseBounds[0, 0, 0] - dose.xValues[x] <= 2.5))
                {
                    organDoseBounds[0, 1, 0] = x;
                }
                if ((dose.xValues[x] - organDoseBounds[0, 0, 1] > 0) && (dose.xValues[x] - organDoseBounds[0, 0, 1] <= 2.5))
                {
                    organDoseBounds[0, 1, 1] = x;
                }
            }
            for (int y = 0; y < dose.yValues.Length; y++)
            {
                if ((organDoseBounds[1, 0, 0] - dose.yValues[y] > 0) && (organDoseBounds[1, 0, 0] - dose.yValues[y] <= 2.5))
                {
                    organDoseBounds[1, 1, 0] = y;
                }
                if ((dose.yValues[y] - organDoseBounds[1, 0, 1] > 0) && (dose.yValues[y] - organDoseBounds[1, 0, 1] <= 2.5))
                {
                    organDoseBounds[1, 1, 1] = y;
                }
            }
            for (int z = 0; z < dose.zValues.Length; z++)
            {
                if ((organDoseBounds[2, 0, 0] - dose.zValues[z] > 0) && (organDoseBounds[2, 0, 0] - dose.zValues[z] <= 2.5))
                {
                    organDoseBounds[2, 1, 0] = z;
                }
                if ((dose.zValues[z] - organDoseBounds[2, 0, 1] > 0) && (dose.zValues[z] - organDoseBounds[2, 0, 1] <= 2.5))
                {
                    organDoseBounds[2, 1, 1] = z;
                }
            }
            return organDoseBounds;
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
