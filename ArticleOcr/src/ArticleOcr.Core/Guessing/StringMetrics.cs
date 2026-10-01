using System;
using System.Collections.Generic;

namespace ArticleOcr.Guessing
{
    /// <summary>String similarity measures used by the guesser.</summary>
    public static class StringMetrics
    {
        /// <summary>
        /// Damerau-Levenshtein distance (optimal string alignment variant) where the cost of every edit comes
        /// from the OCR confusion model instead of being a flat 1.
        /// <para>
        /// d[i,j] = min( d[i-1,j] + del(a_i), d[i,j-1] + ins(b_j), d[i-1,j-1] + sub(a_i,b_j),
        ///               d[i-2,j-2] + transpose  when a_i=b_{j-1} and a_{i-1}=b_j )
        /// </para>
        /// Comparison is case-insensitive. Result is 0 for identical strings.
        /// </summary>
        public static double WeightedEditDistance(string observed, string target, OcrConfusionModel model)
        {
            if (model == null) model = OcrConfusionModel.Default;
            observed = observed ?? string.Empty;
            target = target ?? string.Empty;
            int n = observed.Length, m = target.Length;
            if (n == 0) return SumInsert(target, model);
            if (m == 0) return SumDelete(observed, model);

            var d = new double[n + 1, m + 1];
            d[0, 0] = 0;
            for (int i = 1; i <= n; i++) d[i, 0] = d[i - 1, 0] + model.CostOfDeleting(observed[i - 1]);
            for (int j = 1; j <= m; j++) d[0, j] = d[0, j - 1] + model.CostOfInserting(target[j - 1]);

            for (int i = 1; i <= n; i++)
            {
                char a = observed[i - 1];
                for (int j = 1; j <= m; j++)
                {
                    char b = target[j - 1];
                    double best = d[i - 1, j - 1] + model.SubstitutionCost(a, b);
                    double del = d[i - 1, j] + model.CostOfDeleting(a);
                    if (del < best) best = del;
                    double ins = d[i, j - 1] + model.CostOfInserting(b);
                    if (ins < best) best = ins;
                    if (i > 1 && j > 1 &&
                        char.ToUpperInvariant(a) == char.ToUpperInvariant(target[j - 2]) &&
                        char.ToUpperInvariant(observed[i - 2]) == char.ToUpperInvariant(b))
                    {
                        double tr = d[i - 2, j - 2] + model.TransposeCost;
                        if (tr < best) best = tr;
                    }
                    d[i, j] = best;
                }
            }
            return d[n, m];
        }

        /// <summary>Weighted edit distance divided by the longer length, 0..~1.</summary>
        public static double NormalizedEditDistance(string observed, string target, OcrConfusionModel model)
        {
            int len = Math.Max((observed ?? "").Length, (target ?? "").Length);
            if (len == 0) return 0;
            return WeightedEditDistance(observed, target, model) / len;
        }

        private static double SumInsert(string s, OcrConfusionModel m)
        {
            double t = 0;
            foreach (var c in s) t += m.CostOfInserting(c);
            return t;
        }

        private static double SumDelete(string s, OcrConfusionModel m)
        {
            double t = 0;
            foreach (var c in s) t += m.CostOfDeleting(c);
            return t;
        }

        /// <summary>Jaro similarity, 0..1.</summary>
        public static double Jaro(string s1, string s2)
        {
            s1 = (s1 ?? "").ToUpperInvariant();
            s2 = (s2 ?? "").ToUpperInvariant();
            if (s1.Length == 0 && s2.Length == 0) return 1.0;
            if (s1.Length == 0 || s2.Length == 0) return 0.0;
            if (s1 == s2) return 1.0;

            int matchWindow = Math.Max(0, Math.Max(s1.Length, s2.Length) / 2 - 1);
            var m1 = new bool[s1.Length];
            var m2 = new bool[s2.Length];
            int matches = 0;
            for (int i = 0; i < s1.Length; i++)
            {
                int lo = Math.Max(0, i - matchWindow);
                int hi = Math.Min(s2.Length - 1, i + matchWindow);
                for (int j = lo; j <= hi; j++)
                {
                    if (m2[j] || s1[i] != s2[j]) continue;
                    m1[i] = m2[j] = true;
                    matches++;
                    break;
                }
            }
            if (matches == 0) return 0.0;

            int t = 0, k = 0;
            for (int i = 0; i < s1.Length; i++)
            {
                if (!m1[i]) continue;
                while (!m2[k]) k++;
                if (s1[i] != s2[k]) t++;
                k++;
            }
            double transpositions = t / 2.0;
            return (matches / (double)s1.Length + matches / (double)s2.Length + (matches - transpositions) / matches) / 3.0;
        }

        /// <summary>Jaro-Winkler similarity (prefix scale 0.1, max prefix 4), 0..1.</summary>
        public static double JaroWinkler(string s1, string s2)
        {
            double j = Jaro(s1, s2);
            if (j <= 0.7) return j;
            s1 = (s1 ?? "").ToUpperInvariant();
            s2 = (s2 ?? "").ToUpperInvariant();
            int prefix = 0;
            int max = Math.Min(4, Math.Min(s1.Length, s2.Length));
            while (prefix < max && s1[prefix] == s2[prefix]) prefix++;
            return j + prefix * 0.1 * (1 - j);
        }

        /// <summary>Cosine similarity of character n-gram count vectors (default bigrams), 0..1.</summary>
        public static double NGramCosine(string s1, string s2, int n = 2)
        {
            var v1 = NGrams(s1, n);
            var v2 = NGrams(s2, n);
            if (v1.Count == 0 || v2.Count == 0) return 0;
            double dot = 0, n1 = 0, n2 = 0;
            foreach (var kv in v1)
            {
                n1 += kv.Value * kv.Value;
                int c;
                if (v2.TryGetValue(kv.Key, out c)) dot += kv.Value * c;
            }
            foreach (var kv in v2) n2 += kv.Value * kv.Value;
            return dot / (Math.Sqrt(n1) * Math.Sqrt(n2));
        }

        private static Dictionary<string, int> NGrams(string s, int n)
        {
            var d = new Dictionary<string, int>();
            s = "^" + (s ?? "").ToUpperInvariant() + "$";
            for (int i = 0; i + n <= s.Length; i++)
            {
                var g = s.Substring(i, n);
                int c;
                d.TryGetValue(g, out c);
                d[g] = c + 1;
            }
            return d;
        }
    }
}
