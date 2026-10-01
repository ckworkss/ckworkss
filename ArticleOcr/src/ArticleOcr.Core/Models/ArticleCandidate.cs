using System.Collections.Generic;

namespace ArticleOcr.Models
{
    /// <summary>Where a candidate article code came from.</summary>
    public enum CandidateSource
    {
        /// <summary>The "Article" line of the label.</summary>
        ArticleField,
        /// <summary>The "Quality" line (on many labels Article == Quality).</summary>
        QualityField,
        /// <summary>Any other token in the OCR text that looks like an article code.</summary>
        RawToken,
        /// <summary>Text typed by a user (no OCR involved).</summary>
        UserText
    }

    /// <summary>How the final code was derived from the observed text.</summary>
    public enum CandidateMethod
    {
        /// <summary>Observed text matches a known code exactly.</summary>
        Exact,
        /// <summary>Observed text was matched to a known code using the weighted edit distance / Bayesian model.</summary>
        MasterList,
        /// <summary>No known code matched; the string was repaired with the OCR confusion model so it fits the code grammar.</summary>
        GrammarRepair,
        /// <summary>Observed text fits the grammar but is not in the master list.</summary>
        GrammarOnly,
        /// <summary>Nothing could be verified; the observed text is returned as-is.</summary>
        Unverified
    }

    public sealed class ArticleCandidate
    {
        /// <summary>The proposed article code (normalised, upper case).</summary>
        public string Code { get; set; }

        /// <summary>The text that was actually observed before repair / matching.</summary>
        public string Observed { get; set; }

        public CandidateSource Source { get; set; }
        public CandidateMethod Method { get; set; }

        /// <summary>Confusion-weighted Damerau-Levenshtein distance between Observed and Code (0 = identical).</summary>
        public double Distance { get; set; }

        /// <summary>Jaro-Winkler similarity between Observed and Code (1 = identical).</summary>
        public double JaroWinkler { get; set; }

        /// <summary>Mean OCR confidence of the observed token, 0..1.</summary>
        public double OcrConfidence { get; set; }

        /// <summary>True when Code fits one of the configured article-code patterns.</summary>
        public bool MatchesGrammar { get; set; }

        /// <summary>Unnormalised Bayesian weight (prior x likelihood x evidence).</summary>
        public double Weight { get; set; }

        /// <summary>Posterior probability after normalising over all candidates (0..1).</summary>
        public double Probability { get; set; }

        /// <summary>Number of independent observations (OCR passes / fields) that produced this code.</summary>
        public int Votes { get; set; } = 1;

        public List<string> Notes { get; } = new List<string>();

        public override string ToString()
        {
            return Code + " p=" + Probability.ToString("0.000") + " d=" + Distance.ToString("0.00") + " " + Method + " via " + Source;
        }
    }
}
