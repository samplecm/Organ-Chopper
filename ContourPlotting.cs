using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


namespace DicomChopper
{
    public class ContourPlotting
    {
        public static void Plot(List<double[,]> contours)
        {
            string path = Directory.GetCurrentDirectory();
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(path, "contours.txt")))
            {
                
                for (int i = 0; i < contours.Count; i++)
                {
                    int row = 0;
                    for (int j = 0; j < contours[i].Length; j++)
                    {
                        double value = contours[i][row, j % 3];
                        if (j % 3 == 2)
                        {
                            outputFile.WriteLine(value + " ");
                        }
                        else
                        {
                            outputFile.Write(value + " ");
                        }
                        if (j % 3 == 2) //new line after every z value
                        {
                            row++;
                        }
                    }
                    outputFile.WriteLine(Environment.NewLine);
                    outputFile.WriteLine(Environment.NewLine);
                }
            }
            GnuPlot.SPlot("contours.txt");
            Console.ReadLine();
        }
        public static void Plot(List<List<double[,]>> contours)
        {
            string path = Directory.GetCurrentDirectory();

            using (StreamWriter outputFile = new StreamWriter(Path.Combine(path, "contours.txt")))
            {
               for (int cont = 0; cont < contours.Count; cont++)
               //for (int cont = 9; cont < 12; cont++)
                {
                    for (int i = 0; i < contours[cont].Count; i++)
                    {
                        int row = 0;
                        for (int j = 0; j < contours[cont][i].Length; j++)
                        {
                            double value = contours[cont][i][row, j % 3];
                            if (j % 3 == 2)
                            {
                                outputFile.WriteLine(value + " ");
                            }
                            else
                            {
                                outputFile.Write(value + " ");
                            }
                            if (j % 3 == 2) //new line after every z value
                            {
                                row++;
                            }
                        }
                        outputFile.WriteLine(Environment.NewLine);
                        outputFile.WriteLine(Environment.NewLine);
                    }
                }
            }
            GnuPlot.SPlot("contours.txt");
            GnuPlot.Set("xlabel 'x'");
            GnuPlot.Set("ylabel 'y'");
        }
    }
}
