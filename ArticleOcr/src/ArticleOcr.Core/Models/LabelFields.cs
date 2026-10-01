using System.Collections.Generic;

namespace ArticleOcr.Models
{
    /// <summary>
    /// Fields read from a fabric roll label (Best Pacific style: Quality / Color / Customer / Batch / Roll /
    /// barcode / Width / QTY / Gross / Net / PO No. / Article / Color name / Remark).
    /// Every value is the cleaned OCR text; <c>null</c> means the field was not found.
    /// </summary>
    public sealed class LabelFields
    {
        public string Quality { get; set; }
        public string ColorCode { get; set; }
        public string Customer { get; set; }
        public string Batch { get; set; }
        public string Roll { get; set; }
        public string Barcode { get; set; }
        public string Width { get; set; }
        public string Quantity { get; set; }
        public string Gross { get; set; }
        public string Net { get; set; }
        public string Weight { get; set; }
        public string Free { get; set; }
        public string PoNo { get; set; }
        public string BpOrderNo { get; set; }
        public string Article { get; set; }
        public string ColorName { get; set; }
        public string Remark { get; set; }
        public string Date { get; set; }

        /// <summary>Numeric interpretations (null when the text could not be parsed).</summary>
        public double? WidthCm { get; set; }
        public double? QuantityMeters { get; set; }
        public double? GrossKg { get; set; }
        public double? NetKg { get; set; }
        public double? GramsPerSquareMeter { get; set; }

        /// <summary>
        /// Ratio between the net weight the label states and the net weight implied by
        /// GSM x width x length. Close to 1.0 means the numeric fields agree with each other.
        /// </summary>
        public double? WeightConsistency { get; set; }

        /// <summary>Raw key/value pairs in the order they appeared, for debugging.</summary>
        public List<KeyValuePair<string, string>> RawPairs { get; } = new List<KeyValuePair<string, string>>();

        public IDictionary<string, string> ToDictionary()
        {
            var d = new Dictionary<string, string>();
            Add(d, "Quality", Quality);
            Add(d, "ColorCode", ColorCode);
            Add(d, "Customer", Customer);
            Add(d, "Batch", Batch);
            Add(d, "Roll", Roll);
            Add(d, "Barcode", Barcode);
            Add(d, "Width", Width);
            Add(d, "Quantity", Quantity);
            Add(d, "Gross", Gross);
            Add(d, "Net", Net);
            Add(d, "Weight", Weight);
            Add(d, "Free", Free);
            Add(d, "PoNo", PoNo);
            Add(d, "BpOrderNo", BpOrderNo);
            Add(d, "Article", Article);
            Add(d, "ColorName", ColorName);
            Add(d, "Remark", Remark);
            Add(d, "Date", Date);
            return d;
        }

        private static void Add(IDictionary<string, string> d, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) d[key] = value;
        }
    }
}
