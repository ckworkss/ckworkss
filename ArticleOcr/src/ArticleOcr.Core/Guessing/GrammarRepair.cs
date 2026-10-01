using System;
using System.Collections.Generic;
using System.Linq;
using ArticleOcr.Models;

namespace ArticleOcr.Guessing
{
    /// <summary>
    /// Repairs an OCR string so that it fits the article-code grammar.
    /// <para>
    /// Each observed character is expanded into the set of characters it could really be (from the engine's
    /// own alternative choices when available, otherwise from the confusion model). A beam search then walks
    /// the string position by position, scoring every partial hypothesis with
    /// </para>
    /// <code>
    ///   score = sum_i [ log P(obs_i | out_i)                      (emission: OCR confusion / engine confidence)
    ///                 + log 3 P(class(out_i) | position i)         (positional prior learned from the patterns / master list)
    ///                 + log 3 P(class(out_i) | class(out_i-1)) ]   (class transition model; x3 = ratio to uniform)
    /// </code>
    /// This is a Viterbi-style decode over a hidden-Markov model whose hidden states are character classes and
    /// whose emissions are the confusion probabilities, with the beam keeping the best K hypotheses so the
    /// regex check can be applied to complete strings. Noise characters may be dropped at a cost.
    /// </summary>
    public sealed class GrammarRepair
    {
        private readonly ArticleCodeGrammar _grammar;
        private readonly OcrConfusionModel _confusion;

        public int BeamWidth { get; set; } = 64;
        public int MaxAlternativesPerChar { get; set; } = 4;
        /// <summary>Emission floor so unlikely alternatives are still representable in log space.</summary>
        public double MinEmission { get; set; } = 0.02;
        public double DropNoiseLogCost { get; set; } = Math.Log(0.5);
        public double DropCharLogCost { get; set; } = Math.Log(0.03);

        public GrammarRepair(ArticleCodeGrammar grammar, OcrConfusionModel confusion)
        {
            _grammar = grammar ?? new ArticleCodeGrammar();
            _confusion = confusion ?? OcrConfusionModel.Default;
        }

        private sealed class Hyp
        {
            public string Text;
            public double LogScore;
            public int PrevClass;
        }

        public sealed class RepairResult
        {
            public string Code;
            public double LogScore;
            public bool MatchesGrammar;
            public override string ToString() { return Code + " (" + LogScore.ToString("0.00") + (MatchesGrammar ? ", valid)" : ")"); }
        }

        /// <summary>Repair from plain text using the static confusion model only.</summary>
        public IList<RepairResult> Repair(string observed, int maxResults = 5)
        {
            return RepairCore(ToSymbols(observed), maxResults);
        }

        /// <summary>Repair using per-character engine alternatives (richer emission model).</summary>
        public IList<RepairResult> Repair(IList<OcrSymbol> symbols, int maxResults = 5)
        {
            return RepairCore(symbols, maxResults);
        }

        private static List<OcrSymbol> ToSymbols(string s)
        {
            var list = new List<OcrSymbol>();
            foreach (var c in s ?? string.Empty)
                list.Add(new OcrSymbol { Char = char.ToUpperInvariant(c), Confidence = 0.6 });
            return list;
        }

        private IList<RepairResult> RepairCore(IList<OcrSymbol> symbols, int maxResults)
        {
            if (symbols == null || symbols.Count == 0) return new List<RepairResult>();

            var beam = new List<Hyp> { new Hyp { Text = string.Empty, LogScore = 0, PrevClass = -1 } };

            foreach (var sym in symbols)
            {
                var next = new List<Hyp>();
                var alts = ExpandAlternatives(sym);
                foreach (var h in beam)
                {
                    int pos = h.Text.Length;
                    // keep / substitute the character
                    foreach (var alt in alts)
                    {
                        if (pos >= _grammar.MaxLength + 2) break;
                        int cls = ArticleCodeGrammar.ClassOf(alt.Key);
                        // Prior and transition enter as likelihood ratios against a uniform class model
                        // (log 3P) so that they do not penalise length and make deletions look cheap.
                        double s = h.LogScore + Math.Log(Math.Max(MinEmission, alt.Value))
                                   + Math.Log(3.0 * _grammar.ClassPrior(pos, cls));
                        if (h.PrevClass >= 0) s += Math.Log(3.0 * _grammar.ClassTransition(h.PrevClass, cls));
                        next.Add(new Hyp { Text = h.Text + alt.Key, LogScore = s, PrevClass = cls });
                    }
                    // drop the character (noise is cheap, real characters are expensive; a hyphen is part of many codes)
                    double drop = _confusion.IsNoise(sym.Char) && sym.Char != '-' ? DropNoiseLogCost : DropCharLogCost;
                    next.Add(new Hyp { Text = h.Text, LogScore = h.LogScore + drop, PrevClass = h.PrevClass });
                }
                // de-duplicate identical texts keeping the best score, then prune to the beam width
                beam = next.GroupBy(x => x.Text)
                           .Select(g => g.OrderByDescending(x => x.LogScore).First())
                           .OrderByDescending(x => x.LogScore)
                           .Take(BeamWidth)
                           .ToList();
            }

            var results = beam
                .Where(h => h.Text.Length > 0)
                .Select(h => new RepairResult { Code = h.Text, LogScore = h.LogScore, MatchesGrammar = _grammar.IsValid(h.Text) })
                .OrderByDescending(r => r.MatchesGrammar)
                .ThenByDescending(r => r.LogScore)
                .ToList();

            // Prefer grammar-valid results; if none exist return the best-scoring strings anyway.
            var valid = results.Where(r => r.MatchesGrammar).Take(maxResults).ToList();
            if (valid.Count > 0) return valid;
            return results.Take(maxResults).ToList();
        }

        private List<KeyValuePair<char, double>> ExpandAlternatives(OcrSymbol sym)
        {
            var map = new Dictionary<char, double>();
            char obs = char.ToUpperInvariant(sym.Char);

            // Engine's own alternatives (already 0..1), when present.
            foreach (var c in sym.Choices)
            {
                char u = char.ToUpperInvariant(c.Key);
                double v = Math.Max(MinEmission, c.Value);
                double existing;
                if (!map.TryGetValue(u, out existing) || v > existing) map[u] = v;
            }
            // Confusion-model alternatives, scaled by how unsure the engine was about this symbol.
            double unsure = 1.0 - Clamp(sym.Confidence, 0.0, 0.98);
            foreach (var kv in _confusion.Alternatives(obs))
            {
                double v = kv.Key == obs ? Math.Max(sym.Confidence, 0.3) : kv.Value * Math.Max(unsure, 0.25);
                double existing;
                if (!map.TryGetValue(kv.Key, out existing) || v > existing) map[kv.Key] = v;
            }
            if (!map.ContainsKey(obs)) map[obs] = Math.Max(sym.Confidence, 0.3);

            return map.OrderByDescending(kv => kv.Value).Take(MaxAlternativesPerChar).ToList();
        }

        private static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }
    }
}
