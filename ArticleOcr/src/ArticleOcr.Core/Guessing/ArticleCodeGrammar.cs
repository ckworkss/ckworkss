using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ArticleOcr.Guessing
{
    /// <summary>
    /// Describes what a valid article code looks like: a list of regular expressions plus a positional
    /// character-class profile that can be learned from the master list.
    /// <para>
    /// Default patterns cover the Best Pacific style codes seen on the labels:
    /// <c>RD258150</c> (2 letters + 6 digits), <c>EA511872FX30</c> (2 letters + 6 digits + 2 letters + 2 digits)
    /// and <c>TE181065FX2-A2</c> (same with a dash suffix).
    /// </para>
    /// </summary>
    public sealed class ArticleCodeGrammar
    {
        public static readonly string[] DefaultPatterns =
        {
            @"^[A-Z]{2}\d{6}$",
            @"^[A-Z]{2}\d{6}[A-Z]{1,2}\d{1,3}$",
            @"^[A-Z]{2}\d{6}[A-Z]{1,2}\d{1,3}-[A-Z]\d{1,2}$",
            @"^[A-Z]{1,3}\d{5,8}$"
        };

        private readonly List<Regex> _patterns = new List<Regex>();

        // Positional class profile learned from known codes: for each position the probability of
        // Letter / Digit / Dash. Positions beyond the learned length fall back to the last learned position.
        private double[,] _profile;      // [position, class]
        private double[,] _transition;   // [prevClass, class]
        private int _minLength = 6, _maxLength = 16;

        public const int ClassLetter = 0, ClassDigit = 1, ClassOther = 2;

        public ArticleCodeGrammar() : this(DefaultPatterns) { }

        public ArticleCodeGrammar(IEnumerable<string> patterns)
        {
            foreach (var p in patterns ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                _patterns.Add(new Regex(p.Trim(), RegexOptions.Compiled | RegexOptions.CultureInvariant));
            }
            if (_patterns.Count == 0)
                foreach (var p in DefaultPatterns) _patterns.Add(new Regex(p, RegexOptions.Compiled));
            BuildUniformProfile();
            _exemplars = _patterns.SelectMany(p => PatternExemplars.Expand(p.ToString())).Distinct().ToList();
            Learn(null);
        }

        private List<string> _exemplars = new List<string>();

        /// <summary>Synthetic codes generated from the patterns (e.g. "AA000000", "AA000000A0").</summary>
        public IEnumerable<string> Exemplars { get { return _exemplars; } }

        public int MinLength { get { return _minLength; } }
        public int MaxLength { get { return _maxLength; } }
        public IEnumerable<string> Patterns { get { return _patterns.Select(p => p.ToString()); } }

        public bool IsValid(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            foreach (var p in _patterns) if (p.IsMatch(code)) return true;
            return false;
        }

        /// <summary>Quick pre-filter: could this token be an article code at all?</summary>
        public bool IsPlausibleToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (token.Length < _minLength - 1 || token.Length > _maxLength + 2) return false;
            int digits = token.Count(char.IsDigit);
            int letters = token.Count(char.IsLetter);
            if (digits < 3 || letters < 1) return false;
            if (token.IndexOfAny(new[] { '.', ',', ':', ';', '%', '"', '\'' }) >= 0) return false;   // 48.00Y, 62.20"
            if (!char.IsLetterOrDigit(token[0])) return false;
            return digits + letters >= token.Length - 1;
        }

        public static int ClassOf(char c)
        {
            if (char.IsLetter(c)) return ClassLetter;
            if (char.IsDigit(c)) return ClassDigit;
            return ClassOther;
        }

        private void BuildUniformProfile()
        {
            _profile = new double[_maxLength, 3];
            for (int i = 0; i < _maxLength; i++) { _profile[i, 0] = 0.4; _profile[i, 1] = 0.5; _profile[i, 2] = 0.1; }
            _transition = new double[3, 3];
            for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) _transition[a, b] = 1.0 / 3;
        }

        /// <summary>
        /// Learn the positional class profile and class-transition matrix from known article codes
        /// (additive / Laplace smoothing so unseen classes keep a small probability). Exemplars generated
        /// from the regex patterns are always included so the grammar is known even without a master list.
        /// </summary>
        public void Learn(IEnumerable<string> knownCodes)
        {
            var codes = (knownCodes ?? Enumerable.Empty<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim().ToUpperInvariant()).ToList();
            // real codes decide the length range; exemplars only do so when there is no master list
            var lengthSource = codes.Count > 0 ? codes.ToList() : _exemplars;
            codes.AddRange(_exemplars);
            if (codes.Count == 0) return;
            _minLength = Math.Max(3, lengthSource.Min(c => c.Length));
            _maxLength = Math.Max(_minLength, lengthSource.Max(c => c.Length));

            int profileLength = Math.Max(_maxLength, codes.Max(c => c.Length));
            var counts = new double[profileLength, 3];
            var trans = new double[3, 3];
            for (int i = 0; i < profileLength; i++) for (int k = 0; k < 3; k++) counts[i, k] = 0.5;   // smoothing
            for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) trans[a, b] = 0.5;

            foreach (var code in codes)
            {
                int prev = -1;
                for (int i = 0; i < code.Length; i++)
                {
                    int cls = ClassOf(code[i]);
                    counts[i, cls] += 1;
                    if (prev >= 0) trans[prev, cls] += 1;
                    prev = cls;
                }
            }
            _profile = new double[profileLength, 3];
            for (int i = 0; i < profileLength; i++)
            {
                double s = counts[i, 0] + counts[i, 1] + counts[i, 2];
                for (int k = 0; k < 3; k++) _profile[i, k] = counts[i, k] / s;
            }
            _transition = new double[3, 3];
            for (int a = 0; a < 3; a++)
            {
                double s = trans[a, 0] + trans[a, 1] + trans[a, 2];
                for (int b = 0; b < 3; b++) _transition[a, b] = trans[a, b] / s;
            }
        }

        /// <summary>P(class at position). Positions beyond the profile reuse the last position.</summary>
        public double ClassPrior(int position, int cls)
        {
            int p = Math.Min(position, _profile.GetLength(0) - 1);
            return _profile[p, cls];
        }

        public double ClassTransition(int prevCls, int cls)
        {
            return _transition[prevCls, cls];
        }

        /// <summary>
        /// Log-probability that <paramref name="code"/> is shaped like an article code under the learned
        /// profile + transition model (higher is better). Does not consider the regex patterns.
        /// </summary>
        public double ShapeLogLikelihood(string code)
        {
            if (string.IsNullOrEmpty(code)) return double.NegativeInfinity;
            double ll = 0;
            int prev = -1;
            for (int i = 0; i < code.Length; i++)
            {
                int cls = ClassOf(code[i]);
                ll += Math.Log(ClassPrior(i, cls));
                if (prev >= 0) ll += Math.Log(ClassTransition(prev, cls));
                prev = cls;
            }
            return ll;
        }

        /// <summary>Describe the profile as a shape string such as "LLDDDDDDLLDD" (for diagnostics).</summary>
        public string DescribeProfile()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _profile.GetLength(0); i++)
            {
                double l = _profile[i, 0], d = _profile[i, 1], o = _profile[i, 2];
                sb.Append(l >= d && l >= o ? 'L' : d >= o ? 'D' : '-');
            }
            return sb.ToString();
        }
    }
}
