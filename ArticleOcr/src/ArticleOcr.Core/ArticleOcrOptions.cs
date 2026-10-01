using System.Collections.Generic;

namespace ArticleOcr
{
    /// <summary>Configuration for <see cref="ArticleOcrService"/>.</summary>
    public sealed class ArticleOcrOptions
    {
        /// <summary>Folder containing <c>eng.traineddata</c>.</summary>
        public string TessDataPath { get; set; }
        public string Language { get; set; } = "eng";
        /// <summary>Optional CSV of known article codes (Code,Description,Count).</summary>
        public string MasterListPath { get; set; }
        /// <summary>Regular expressions that a valid article code must match (any of).</summary>
        public List<string> ArticlePatterns { get; set; } = new List<string>();
        public int MaxImageDimension { get; set; } = 2200;
        public int MinImageDimension { get; set; } = 1400;
        /// <summary>Try the 90/180/270 degree rotations when the best pass is weaker than this (0..1).</summary>
        public double RotationFallbackConfidence { get; set; } = 0.35;
        /// <summary>Maximum concurrent Tesseract engines.</summary>
        public int MaxEngines { get; set; } = 0;
        public double Lambda { get; set; } = 2.0;
        public double VerifiedThreshold { get; set; } = 0.60;
        public double VerifiedMargin { get; set; } = 0.25;
        /// <summary>Only report "verified" for codes that exist in the master list.</summary>
        public bool RequireMasterListForVerification { get; set; } = false;
    }
}
