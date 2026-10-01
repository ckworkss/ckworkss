using System;
using System.Collections.Generic;

namespace ArticleOcr.Guessing
{
    /// <summary>
    /// Visual confusion model for printed label fonts.
    /// <para>
    /// <see cref="Similarity"/>(a, b) is the probability-like score (0..1) that the OCR engine prints <c>b</c>
    /// when the real character is <c>a</c>. <see cref="SubstitutionCost"/> is <c>1 - Similarity</c>, so swapping
    /// '0' for 'O' costs only 0.15 while swapping '0' for 'W' costs the full 1.0.
    /// </para>
    /// The numbers are hand-tuned priors for dot-matrix / thermal label fonts. They can be refined from
    /// real data by calling <see cref="Set"/>.
    /// </summary>
    public sealed class OcrConfusionModel
    {
        private readonly Dictionary<int, double> _sim = new Dictionary<int, double>();

        /// <summary>Characters that are usually OCR noise (dirt, dividers, drop-outs). Deleting them is cheap.</summary>
        public const string NoiseChars = " .,:;'`\"-_|~^*°·¨´‘’“”!()[]{}<>/\\";

        public double InsertCost { get; set; } = 1.0;
        public double DeleteCost { get; set; } = 1.0;
        public double NoiseDeleteCost { get; set; } = 0.30;
        public double TransposeCost { get; set; } = 0.80;
        /// <summary>Cost of a substitution between two characters that are not in the confusion table.</summary>
        public double DefaultSubstitutionCost { get; set; } = 1.0;
        /// <summary>Cost of a substitution within the same class (digit->digit, letter->letter) without a table entry.</summary>
        public double SameClassSubstitutionCost { get; set; } = 0.85;

        public static OcrConfusionModel Default { get; } = CreateDefault();

        public static OcrConfusionModel CreateDefault()
        {
            var m = new OcrConfusionModel();
            // digits <-> letters
            m.Set('0', 'O', 0.85); m.Set('0', 'D', 0.55); m.Set('0', 'Q', 0.50); m.Set('0', 'U', 0.25); m.Set('0', 'C', 0.25);
            m.Set('1', 'I', 0.85); m.Set('1', 'L', 0.55); m.Set('1', 'T', 0.35); m.Set('1', '7', 0.40); m.Set('1', 'J', 0.30);
            m.Set('2', 'Z', 0.80); m.Set('2', '7', 0.30); m.Set('2', 'S', 0.20);
            m.Set('3', 'B', 0.40); m.Set('3', 'E', 0.35); m.Set('3', '8', 0.40);
            m.Set('4', 'A', 0.55); m.Set('4', 'H', 0.25); m.Set('4', '9', 0.20);
            m.Set('5', 'S', 0.85); m.Set('5', '6', 0.35); m.Set('5', '3', 0.25);
            m.Set('6', 'G', 0.65); m.Set('6', 'B', 0.35); m.Set('6', '8', 0.35); m.Set('6', 'C', 0.25); m.Set('6', '0', 0.25);
            m.Set('7', 'T', 0.55); m.Set('7', 'Z', 0.40); m.Set('7', 'Y', 0.25);
            m.Set('8', 'B', 0.85); m.Set('8', 'S', 0.30); m.Set('8', '3', 0.35); m.Set('8', '0', 0.30);
            m.Set('9', 'G', 0.40); m.Set('9', 'Q', 0.30); m.Set('9', '0', 0.30); m.Set('9', 'P', 0.25);
            // letters <-> letters
            m.Set('A', 'R', 0.35); m.Set('B', 'R', 0.40); m.Set('B', 'E', 0.30); m.Set('B', 'D', 0.35);
            m.Set('C', 'G', 0.55); m.Set('C', 'O', 0.45); m.Set('C', 'E', 0.30);
            m.Set('D', 'O', 0.55); m.Set('D', 'Q', 0.35); m.Set('D', 'P', 0.30);
            m.Set('E', 'F', 0.60); m.Set('E', 'L', 0.35);
            m.Set('F', 'P', 0.50); m.Set('F', 'T', 0.30); m.Set('F', 'R', 0.25);
            m.Set('G', 'Q', 0.35); m.Set('G', 'O', 0.30);
            m.Set('H', 'N', 0.50); m.Set('H', 'M', 0.35); m.Set('H', 'K', 0.35);
            m.Set('I', 'L', 0.45); m.Set('I', 'T', 0.30); m.Set('I', 'J', 0.35);
            m.Set('K', 'X', 0.50); m.Set('K', 'R', 0.40);
            m.Set('M', 'N', 0.50); m.Set('M', 'W', 0.30);
            m.Set('N', 'W', 0.25);
            m.Set('O', 'Q', 0.60); m.Set('O', 'U', 0.30);
            m.Set('P', 'R', 0.50);
            m.Set('S', 'Z', 0.25);
            m.Set('T', 'Y', 0.35);
            m.Set('U', 'V', 0.55); m.Set('U', 'J', 0.35);
            m.Set('V', 'Y', 0.50); m.Set('V', 'W', 0.45);
            m.Set('W', 'Y', 0.20);
            m.Set('X', 'Y', 0.35);
            m.Set('Z', 'S', 0.25);
            // noise glyphs that stand in for real characters
            m.Set('I', '|', 0.90); m.Set('1', '|', 0.80); m.Set('I', '!', 0.60); m.Set('1', '!', 0.55); m.Set('L', '|', 0.40);
            m.Set('-', '_', 0.80); m.Set('-', '~', 0.60); m.Set('-', '=', 0.60); m.Set('-', '.', 0.30);
            m.Set('O', '°', 0.40); m.Set('0', '°', 0.40);
            return m;
        }

        private static int Key(char a, char b)
        {
            a = char.ToUpperInvariant(a);
            b = char.ToUpperInvariant(b);
            if (a > b) { var t = a; a = b; b = t; }
            return (a << 16) | b;
        }

        /// <summary>Set a symmetric similarity (0..1) between two characters.</summary>
        public void Set(char a, char b, double similarity)
        {
            if (similarity < 0 || similarity > 1) throw new ArgumentOutOfRangeException(nameof(similarity));
            _sim[Key(a, b)] = similarity;
        }

        public double Similarity(char a, char b)
        {
            if (char.ToUpperInvariant(a) == char.ToUpperInvariant(b)) return 1.0;
            double s;
            if (_sim.TryGetValue(Key(a, b), out s)) return s;
            return 0.0;
        }

        public double SubstitutionCost(char a, char b)
        {
            if (char.ToUpperInvariant(a) == char.ToUpperInvariant(b)) return 0.0;
            double s;
            if (_sim.TryGetValue(Key(a, b), out s)) return 1.0 - s;
            if (char.IsDigit(a) && char.IsDigit(b)) return SameClassSubstitutionCost;
            if (char.IsLetter(a) && char.IsLetter(b)) return SameClassSubstitutionCost;
            return DefaultSubstitutionCost;
        }

        public bool IsNoise(char c)
        {
            return NoiseChars.IndexOf(c) >= 0;
        }

        public double CostOfDeleting(char c)
        {
            return IsNoise(c) ? NoiseDeleteCost : DeleteCost;
        }

        public double CostOfInserting(char c)
        {
            return IsNoise(c) ? NoiseDeleteCost : InsertCost;
        }

        /// <summary>
        /// All characters that <paramref name="observed"/> may really have been, with similarity scores
        /// (the observed character itself is included with score 1.0). Used by the grammar repair beam search.
        /// </summary>
        public IEnumerable<KeyValuePair<char, double>> Alternatives(char observed)
        {
            var o = char.ToUpperInvariant(observed);
            yield return new KeyValuePair<char, double>(o, 1.0);
            foreach (var kv in _sim)
            {
                var a = (char)(kv.Key >> 16);
                var b = (char)(kv.Key & 0xFFFF);
                if (a == o && b != o) yield return new KeyValuePair<char, double>(b, kv.Value);
                else if (b == o && a != o) yield return new KeyValuePair<char, double>(a, kv.Value);
            }
        }
    }
}
