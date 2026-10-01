using System;
using System.Collections.Generic;

namespace ArticleOcr.Models
{
    /// <summary>One recognised symbol (character) with the alternatives the engine considered.</summary>
    public sealed class OcrSymbol
    {
        public char Char { get; set; }
        /// <summary>0..1</summary>
        public double Confidence { get; set; }
        /// <summary>Alternative characters with their 0..1 confidences, best first (may be empty).</summary>
        public List<KeyValuePair<char, double>> Choices { get; } = new List<KeyValuePair<char, double>>();
    }

    public sealed class OcrWord
    {
        public string Text { get; set; }
        /// <summary>0..1</summary>
        public double Confidence { get; set; }
        public int Line { get; set; }
        public List<OcrSymbol> Symbols { get; } = new List<OcrSymbol>();
    }

    /// <summary>Output of a single OCR pass over one preprocessed image variant.</summary>
    public sealed class OcrPassResult
    {
        public string Variant { get; set; }
        public string Text { get; set; }
        /// <summary>0..1</summary>
        public double MeanConfidence { get; set; }
        public List<OcrWord> Words { get; } = new List<OcrWord>();
        public TimeSpan Elapsed { get; set; }
    }

    /// <summary>Final result of <see cref="ArticleOcrService"/>.</summary>
    public sealed class ArticleOcrResult
    {
        /// <summary>Best article code, or null when nothing at all could be extracted.</summary>
        public string ArticleCode { get; set; }

        /// <summary>Posterior probability of <see cref="ArticleCode"/> (0..1).</summary>
        public double Confidence { get; set; }

        /// <summary>True when the code was read directly and verified; false when it is a statistical guess.</summary>
        public bool IsVerified { get; set; }

        public CandidateMethod Method { get; set; }
        public CandidateSource Source { get; set; }

        /// <summary>All candidates ranked by probability.</summary>
        public List<ArticleCandidate> Candidates { get; } = new List<ArticleCandidate>();

        public LabelFields Fields { get; set; }

        /// <summary>Raw text of the best OCR pass.</summary>
        public string RawText { get; set; }

        /// <summary>Name of the preprocessing variant that produced the best pass.</summary>
        public string BestVariant { get; set; }

        /// <summary>Mean OCR confidence of the best pass (0..1).</summary>
        public double OcrConfidence { get; set; }

        public List<OcrPassResult> Passes { get; } = new List<OcrPassResult>();

        public TimeSpan Elapsed { get; set; }

        public List<string> Diagnostics { get; } = new List<string>();
    }
}
