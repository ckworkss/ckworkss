using System;
using System.Collections.Generic;
using System.Linq;
using ArticleOcr.Models;

namespace ArticleOcr.Guessing
{
    /// <summary>One observation of a possible article code handed to the guesser.</summary>
    public sealed class Observation
    {
        public string Text { get; set; }
        public CandidateSource Source { get; set; }
        /// <summary>0..1 OCR confidence of the token (use ~0.6 for typed text).</summary>
        public double Confidence { get; set; } = 0.6;
        /// <summary>Optional per-character engine alternatives.</summary>
        public IList<OcrSymbol> Symbols { get; set; }
    }

    /// <summary>
    /// Turns noisy observations into a ranked list of article codes with posterior probabilities.
    /// <para>
    /// For every observation <c>o</c> and every hypothesis <c>c</c> (a master-list code, a grammar repair, or
    /// the raw text) the unnormalised weight is
    /// </para>
    /// <code>
    ///   w(c | o) = P(c) * exp(-lambda * d(o, c)) * (0.5 + 0.5 * conf(o)) * sourceWeight(o) * grammarFactor(c)
    /// </code>
    /// <list type="bullet">
    ///   <item><c>P(c)</c>: smoothed frequency prior from the master list (uniform when there is no list).</item>
    ///   <item><c>d(o, c)</c>: confusion-weighted Damerau-Levenshtein distance. <c>exp(-lambda d)</c> is the likelihood
    ///   under an exponential noise model: each unit of edit cost makes the hypothesis <c>e^lambda</c> times less likely.</item>
    ///   <item><c>conf(o)</c>: OCR confidence of the observation.</item>
    ///   <item><c>grammarFactor</c>: 1 when the code fits the configured patterns, <see cref="GrammarPenalty"/> otherwise.</item>
    /// </list>
    /// Weights of identical codes from different observations are summed (independent votes), then normalised
    /// into probabilities together with an explicit "none of the above" hypothesis so a bad match does not get
    /// probability 1.0 just because it is the only match.
    /// </summary>
    public sealed class ArticleGuesser
    {
        private readonly ArticleMasterList _master;
        private readonly ArticleCodeGrammar _grammar;
        private readonly OcrConfusionModel _confusion;
        private readonly GrammarRepair _repair;

        /// <summary>Noise model sharpness. 2.0 => one full character substitution is e^2 ~ 7.4x less likely.</summary>
        public double Lambda { get; set; } = 2.0;
        /// <summary>Maximum normalised distance (distance / length) for a master-list code to be considered.</summary>
        public double MaxNormalizedDistance { get; set; } = 0.45;
        /// <summary>Weight multiplier for codes that violate the grammar.</summary>
        public double GrammarPenalty { get; set; } = 0.35;
        /// <summary>Prior mass and virtual distance of the "none of the above" hypothesis.</summary>
        public double UnknownPrior { get; set; } = 0.05;
        public double UnknownDistance { get; set; } = 1.2;
        /// <summary>How much to trust each source relative to the Article line.</summary>
        public double QualityFieldWeight { get; set; } = 0.6;
        public double RawTokenWeight { get; set; } = 0.4;
        /// <summary>Top probability needed to call the answer verified.</summary>
        public double VerifiedThreshold { get; set; } = 0.60;
        /// <summary>Minimum probability margin between first and second candidate for a verified answer.</summary>
        public double VerifiedMargin { get; set; } = 0.25;
        /// <summary>
        /// Largest edit distance between what was read and the reported code for a verified answer. 0.9 allows
        /// several confusable swaps (0/O, 1/I, 5/S ...) and noise, but not a completely unreadable character.
        /// </summary>
        public double MaxVerifiedDistance { get; set; } = 0.9;
        /// <summary>When true, only codes present in the master list can be reported as verified.</summary>
        public bool RequireMasterListForVerification { get; set; } = false;
        public int MaxCandidates { get; set; } = 10;

        public ArticleGuesser(ArticleMasterList master, ArticleCodeGrammar grammar, OcrConfusionModel confusion = null)
        {
            _master = master ?? new ArticleMasterList();
            _grammar = grammar ?? new ArticleCodeGrammar();
            _confusion = confusion ?? OcrConfusionModel.Default;
            _repair = new GrammarRepair(_grammar, _confusion);
            if (_master.Count > 0) _grammar.Learn(_master.Codes);
        }

        public ArticleCodeGrammar Grammar { get { return _grammar; } }
        public ArticleMasterList MasterList { get { return _master; } }

        /// <summary>Convenience overload for a single typed / observed string.</summary>
        public List<ArticleCandidate> Guess(string text, double confidence = 0.6)
        {
            return Guess(new[] { new Observation { Text = text, Source = CandidateSource.UserText, Confidence = confidence } });
        }

        public List<ArticleCandidate> Guess(IEnumerable<Observation> observations)
        {
            var byCode = new Dictionary<string, ArticleCandidate>(StringComparer.OrdinalIgnoreCase);

            foreach (var obs in observations ?? Enumerable.Empty<Observation>())
            {
                var observed = ArticleMasterList.Normalize(obs.Text);
                if (string.IsNullOrEmpty(observed)) continue;
                double srcW = SourceWeight(obs.Source);
                double evidence = 0.5 + 0.5 * Clamp01(obs.Confidence);

                foreach (var hyp in Hypotheses(observed, obs))
                {
                    double dist = StringMetrics.WeightedEditDistance(observed, hyp.Code, _confusion);
                    double prior = _master.Prior(hyp.Code);
                    bool grammarOk = _grammar.IsValid(hyp.Code);
                    double w = prior * Math.Exp(-Lambda * dist) * evidence * srcW * (grammarOk ? 1.0 : GrammarPenalty);
                    if (hyp.Method == CandidateMethod.GrammarRepair) w *= hyp.RepairFactor;

                    ArticleCandidate c;
                    if (byCode.TryGetValue(hyp.Code, out c))
                    {
                        c.Weight += w;
                        c.Votes++;
                        if (dist < c.Distance) { c.Distance = dist; c.Observed = observed; c.Source = obs.Source; c.Method = hyp.Method; }
                        c.OcrConfidence = Math.Max(c.OcrConfidence, obs.Confidence);
                    }
                    else
                    {
                        c = new ArticleCandidate
                        {
                            Code = hyp.Code,
                            Observed = observed,
                            Source = obs.Source,
                            Method = hyp.Method,
                            Distance = dist,
                            JaroWinkler = StringMetrics.JaroWinkler(observed, hyp.Code),
                            OcrConfidence = obs.Confidence,
                            MatchesGrammar = grammarOk,
                            Weight = w
                        };
                        if (hyp.Note != null) c.Notes.Add(hyp.Note);
                        byCode[hyp.Code] = c;
                    }
                }
            }

            // "none of the above": prior mass that competes with weak matches.
            double unknown = UnknownPrior * Math.Exp(-Lambda * UnknownDistance);
            double total = byCode.Values.Sum(c => c.Weight) + unknown;
            foreach (var c in byCode.Values) c.Probability = total > 0 ? c.Weight / total : 0;

            return byCode.Values
                .OrderByDescending(c => c.Probability)
                .ThenBy(c => c.Distance)
                .Take(MaxCandidates)
                .ToList();
        }

        /// <summary>Decide whether the top candidate is trustworthy.</summary>
        public bool IsVerified(IList<ArticleCandidate> ranked)
        {
            if (ranked == null || ranked.Count == 0) return false;
            var top = ranked[0];
            if (top.Probability < VerifiedThreshold) return false;
            if (top.Method == CandidateMethod.Unverified) return false;
            if (top.Distance > MaxVerifiedDistance) return false;
            if (RequireMasterListForVerification && !_master.Contains(top.Code)) return false;
            double second = ranked.Count > 1 ? ranked[1].Probability : 0;
            return top.Probability - second >= VerifiedMargin;
        }

        private double SourceWeight(CandidateSource s)
        {
            switch (s)
            {
                case CandidateSource.QualityField: return QualityFieldWeight;
                case CandidateSource.RawToken: return RawTokenWeight;
                default: return 1.0;
            }
        }

        private sealed class Hypothesis
        {
            public string Code;
            public CandidateMethod Method;
            public double RepairFactor = 1.0;
            public string Note;
        }

        private IEnumerable<Hypothesis> Hypotheses(string observed, Observation obs)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Exact hit in the master list.
            if (_master.Contains(observed))
            {
                seen.Add(observed);
                yield return new Hypothesis { Code = observed, Method = CandidateMethod.Exact };
            }

            // 2. Fuzzy hits in the master list (length pre-filter keeps this fast for big lists).
            foreach (var code in _master.Codes)
            {
                if (seen.Contains(code)) continue;
                if (Math.Abs(code.Length - observed.Length) > 3) continue;
                double nd = StringMetrics.NormalizedEditDistance(observed, code, _confusion);
                if (nd > MaxNormalizedDistance) continue;
                seen.Add(code);
                yield return new Hypothesis { Code = code, Method = CandidateMethod.MasterList };
            }

            // 3. Grammar repair of the observed string (works with or without a master list).
            var repairs = obs.Symbols != null && obs.Symbols.Count > 0 ? _repair.Repair(obs.Symbols, 3) : _repair.Repair(observed, 3);
            double bestLog = repairs.Count > 0 ? repairs[0].LogScore : 0;
            foreach (var r in repairs)
            {
                if (seen.Contains(r.Code)) continue;
                seen.Add(r.Code);
                if (_master.Contains(r.Code))
                {
                    yield return new Hypothesis { Code = r.Code, Method = CandidateMethod.MasterList, Note = "reached via grammar repair" };
                    continue;
                }
                var method = r.MatchesGrammar
                    ? (string.Equals(r.Code, observed, StringComparison.OrdinalIgnoreCase) ? CandidateMethod.GrammarOnly : CandidateMethod.GrammarRepair)
                    : CandidateMethod.Unverified;
                // relative quality of this repair versus the best repair (0..1)
                double factor = Math.Exp(Math.Max(-6, r.LogScore - bestLog));
                yield return new Hypothesis { Code = r.Code, Method = method, RepairFactor = factor, Note = r.MatchesGrammar ? null : "does not fit the article pattern" };
            }

            // 4. The raw observation itself, so a totally unknown but clean code still appears.
            if (!seen.Contains(observed))
            {
                yield return new Hypothesis
                {
                    Code = observed,
                    Method = _grammar.IsValid(observed) ? CandidateMethod.GrammarOnly : CandidateMethod.Unverified
                };
            }
        }

        private static double Clamp01(double v)
        {
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }
    }
}
