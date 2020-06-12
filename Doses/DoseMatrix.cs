using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dicom;


namespace DicomChopper.Doses
{
    public class DoseMatrix
    {
        public double[,,] Matrix;
        public double[] xValues;
        public double[] yValues;
        public double[] zValues;
        public DoseMatrix(DicomDataset doseData)
        {
            this.Matrix = DicomDose.GetDoseMatrix(doseData);
            this.zValues = DicomDose.GetZValues(doseData);
            this.yValues = DicomDose.GetYValues(doseData);
            this.xValues = DicomDose.GetXValues(doseData);
        }

    }
}
