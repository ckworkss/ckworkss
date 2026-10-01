using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ArticleOcr.Guessing;
using ArticleOcr.Models;

namespace ArticleOcr.Parsing
{
    /// <summary>A parsed field with the OCR words that produced it.</summary>
    public sealed class ParsedField
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public double Confidence { get; set; }
        public List<OcrWord> Words { get; } = new List<OcrWord>();

        /// <summary>Symbols of the value words concatenated (for the grammar repair emission model).</summary>
        public List<OcrSymbol> Symbols
        {
            get { return Words.SelectMany(w => w.Symbols).ToList(); }
        }
    }

    /// <summary>
    /// Splits OCR output of a roll label into key/value pairs. Keys are matched fuzzily ("Artic1e", "Qua1ity",
    /// "Co1or") with the OCR confusion model so damaged labels still parse. A line may hold several fields
    /// ("Gross : 6.8KG Net : 6.1KG").
    /// </summary>
    public sealed class LabelParser
    {
        private static readonly Regex BarcodeRx = new Regex(@"(?<!\d)\d{12,14}(?!\d)", RegexOptions.Compiled);
        private static readonly Regex DateRx = new Regex(@"\b(20\d{2})[-/.](\d{2})[-/.](\d{2})\b", RegexOptions.Compiled);
        private static readonly Regex NumberRx = new Regex(@"\d+(?:[.,]\d+)?", RegexOptions.Compiled);

        private sealed class KeyDef
        {
            public string Name;         // canonical field name
            public string[] Aliases;    // letter-only alias spellings, upper case
            public bool Repeatable;     // e.g. "Color" appears twice
        }

        private static readonly KeyDef[] Keys =
        {
            new KeyDef { Name = "Quality",   Aliases = new[] { "QUALITY", "QUALITV" } },
            new KeyDef { Name = "Color",     Aliases = new[] { "COLOR", "COLOUR" }, Repeatable = true },
            new KeyDef { Name = "Customer",  Aliases = new[] { "CUSTOMER" } },
            new KeyDef { Name = "Batch",     Aliases = new[] { "BATCH" } },
            new KeyDef { Name = "Roll",      Aliases = new[] { "ROLL" } },
            new KeyDef { Name = "Width",     Aliases = new[] { "WIDTH" } },
            new KeyDef { Name = "Quantity",  Aliases = new[] { "QTY", "QUANTITY" } },
            new KeyDef { Name = "Gross",     Aliases = new[] { "GROSS" } },
            new KeyDef { Name = "Net",       Aliases = new[] { "NET" } },
            new KeyDef { Name = "Weight",    Aliases = new[] { "WEIGHT" } },
            new KeyDef { Name = "Free",      Aliases = new[] { "FREE" } },
            new KeyDef { Name = "PoNo",      Aliases = new[] { "PONO", "PONUMBER" } },
            new KeyDef { Name = "BpOrderNo", Aliases = new[] { "BPORDERNO", "ORDERNO" } },
            new KeyDef { Name = "Article",   Aliases = new[] { "ARTICLE", "ARTICLENO", "ART" } },
            new KeyDef { Name = "Remark",    Aliases = new[] { "REMARK", "REMARKS" } },
            new KeyDef { Name = "Date",      Aliases = new[] { "DATE" } },
        };

        private readonly OcrConfusionModel _confusion;

        /// <summary>Maximum normalised edit distance for a token to be accepted as a field key.</summary>
        public double KeyMatchTolerance { get; set; } = 0.30;

        public LabelParser(OcrConfusionModel confusion = null)
        {
            _confusion = confusion ?? OcrConfusionModel.Default;
        }

        /// <summary>Parse plain text (no confidences, useful for tests and typed input).</summary>
        public LabelFields Parse(string text)
        {
            return Parse(LinesFromText(text));
        }

        public LabelFields Parse(OcrPassResult pass)
        {
            if (pass == null) return new LabelFields();
            var lines = pass.Words.Count > 0 ? LinesFromWords(pass.Words) : LinesFromText(pass.Text);
            return Parse(lines);
        }

        /// <summary>All parsed fields including repeated keys, with their words and confidences.</summary>
        public List<ParsedField> ParseFields(OcrPassResult pass)
        {
            if (pass == null) return new List<ParsedField>();
            var lines = pass.Words.Count > 0 ? LinesFromWords(pass.Words) : LinesFromText(pass.Text);
            return ExtractFields(lines);
        }

        public LabelFields Parse(IList<IList<OcrWord>> lines)
        {
            var fields = ExtractFields(lines);
            var result = new LabelFields();
            var colorSeen = 0;

            foreach (var f in fields)
            {
                result.RawPairs.Add(new KeyValuePair<string, string>(f.Key, f.Value));
                var v = string.IsNullOrWhiteSpace(f.Value) ? null : f.Value.Trim();
                if (v == null) continue;
                switch (f.Key)
                {
                    case "Quality": result.Quality = result.Quality ?? CleanCode(v); break;
                    case "Color":
                        // first Color line: "EPB80772HPM" or "V01A001A Black 0813" -> code = first token when it holds digits
                        // second Color line (or a first one without digits): colour name
                        if (colorSeen == 0)
                        {
                            var parts = v.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                            if (parts[0].Any(char.IsDigit))
                            {
                                result.ColorCode = CleanCode(parts[0]);
                                if (parts.Length > 1) result.ColorName = parts[1].Trim();
                            }
                            else result.ColorName = v;
                        }
                        else result.ColorName = result.ColorName ?? v;
                        colorSeen++;
                        break;
                    case "Customer": result.Customer = result.Customer ?? CleanCode(v); break;
                    case "Batch": result.Batch = result.Batch ?? CleanCode(v); break;
                    case "Roll": result.Roll = result.Roll ?? CleanCode(v); break;
                    case "Width": result.Width = result.Width ?? v; break;
                    case "Quantity": result.Quantity = result.Quantity ?? v; break;
                    case "Gross": result.Gross = result.Gross ?? v; break;
                    case "Net": result.Net = result.Net ?? v; break;
                    case "Weight": result.Weight = result.Weight ?? v; break;
                    case "Free": result.Free = result.Free ?? v; break;
                    case "PoNo": result.PoNo = result.PoNo ?? CleanCode(v); break;
                    case "BpOrderNo": result.BpOrderNo = result.BpOrderNo ?? CleanCode(v); break;
                    case "Article": result.Article = result.Article ?? CleanCode(v); break;
                    case "Remark": result.Remark = result.Remark ?? v; break;
                    case "Date": result.Date = result.Date ?? v; break;
                }
            }

            var all = string.Join("\n", lines.Select(l => string.Join(" ", l.Select(w => w.Text))));
            var bc = BarcodeRx.Match(all);
            if (bc.Success) result.Barcode = bc.Value;
            var dt = DateRx.Match(all);
            if (dt.Success && result.Date == null) result.Date = dt.Groups[1].Value + "-" + dt.Groups[2].Value + "-" + dt.Groups[3].Value;

            ParseNumbers(result);
            return result;
        }

        // ---------------------------------------------------------------------------------------------

        private List<ParsedField> ExtractFields(IList<IList<OcrWord>> lines)
        {
            var fields = new List<ParsedField>();
            foreach (var line in lines)
            {
                ParsedField current = null;
                int i = 0;
                while (i < line.Count)
                {
                    int consumed;
                    var key = MatchKey(line, i, out consumed);
                    if (key != null)
                    {
                        if (current != null) Finish(current, fields);
                        current = new ParsedField { Key = key };
                        i += consumed;
                        continue;
                    }
                    if (current != null) current.Words.Add(line[i]);
                    i++;
                }
                if (current != null) Finish(current, fields);
            }
            return fields;
        }

        private static void Finish(ParsedField f, List<ParsedField> fields)
        {
            var text = string.Join(" ", f.Words.Select(w => w.Text)).Trim();
            text = DateRx.Replace(text, string.Empty).Trim();   // dates are printed next to other fields
            text = text.TrimStart(':', '.', '-', '_', '|', ';', ',', '\'', '"', ' ', '=');
            f.Value = text.Trim();
            f.Confidence = f.Words.Count == 0 ? 0 : f.Words.Average(w => w.Confidence);
            fields.Add(f);
        }

        private string MatchKey(IList<OcrWord> line, int index, out int consumed)
        {
            consumed = 0;
            var w1 = Letters(line[index].Text);
            if (w1.Length == 0) return null;

            // Two-word keys: "PO No", "P.O. No.", "BP Order No", "Article No"
            if (index + 1 < line.Count)
            {
                var w2 = Letters(line[index + 1].Text);
                if ((w1 == "PO" || w1 == "P0" || w1 == "PQ") && (w2 == "NO" || w2 == "N0" || w2 == "NQ")) { consumed = 2; return "PoNo"; }
                if (w1 == "BP" && FuzzyEquals(w2, "ORDER"))
                {
                    consumed = 2;
                    if (index + 2 < line.Count && Letters(line[index + 2].Text).StartsWith("N")) consumed = 3;
                    return "BpOrderNo";
                }
            }
            if ((w1 == "PONO" || w1 == "P0NO" || w1 == "PON0")) { consumed = 1; return "PoNo"; }

            // Key glued to its value: "Batch:V23016221" -> the word itself holds ':'; split handled by letters-prefix check.
            foreach (var k in Keys)
            {
                foreach (var alias in k.Aliases)
                {
                    if (alias.Length < 3) continue;
                    if (FuzzyEquals(w1, alias))
                    {
                        consumed = 1;
                        return k.Name;
                    }
                }
            }
            return null;
        }

        private bool FuzzyEquals(string token, string alias)
        {
            if (token.Length == 0) return false;
            if (token == alias) return true;
            if (Math.Abs(token.Length - alias.Length) > 2) return false;
            if (alias.Length <= 3) return token == alias;   // short keys (NET, QTY, ART) must match exactly
            double nd = StringMetrics.NormalizedEditDistance(token, alias, _confusion);
            return nd <= KeyMatchTolerance;
        }

        private static string Letters(string s)
        {
            if (s == null) return string.Empty;
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            }
            // digits that are really letters inside a key ("Qua1ity", "C0lor")
            return sb.ToString().Replace('1', 'I').Replace('0', 'O').Replace('5', 'S').Replace('8', 'B');
        }

        /// <summary>Upper-case, remove spaces and stray punctuation around a code value.</summary>
        public static string CleanCode(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return null;
            var s = new string(v.Trim().ToUpperInvariant().Where(c => !char.IsWhiteSpace(c)).ToArray());
            s = s.Trim(':', '.', ',', ';', '-', '_', '|', '\'', '"', '`', '=', '*');
            return s.Length == 0 ? null : s;
        }

        private static IList<IList<OcrWord>> LinesFromText(string text)
        {
            var lines = new List<IList<OcrWord>>();
            if (string.IsNullOrEmpty(text)) return lines;
            int ln = 0;
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var words = raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(t => new OcrWord { Text = t, Confidence = 0.6, Line = ln })
                               .ToList<OcrWord>();
                if (words.Count > 0) lines.Add(words);
                ln++;
            }
            return lines;
        }

        private static IList<IList<OcrWord>> LinesFromWords(IEnumerable<OcrWord> words)
        {
            return words.GroupBy(w => w.Line).OrderBy(g => g.Key).Select(g => (IList<OcrWord>)g.ToList()).ToList();
        }

        // ---------------------------------------------------------------------------------------------

        private static void ParseNumbers(LabelFields f)
        {
            f.WidthCm = FirstNumber(f.Width);
            f.GrossKg = FirstNumber(f.Gross);
            f.NetKg = FirstNumber(f.Net);
            f.GramsPerSquareMeter = FirstNumber(f.Weight);

            var qty = FirstNumber(f.Quantity);
            if (qty.HasValue)
            {
                var unit = (f.Quantity ?? "").ToUpperInvariant();
                int idx = unit.IndexOfAny(new[] { 'Y', 'M' });
                bool yards = idx >= 0 && unit[idx] == 'Y';
                f.QuantityMeters = yards ? qty.Value * 0.9144 : qty.Value;
            }

            // Net weight implied by GSM x width x length; ratio ~1 means the numbers agree.
            if (f.GramsPerSquareMeter.HasValue && f.WidthCm.HasValue && f.QuantityMeters.HasValue && f.NetKg.HasValue && f.NetKg.Value > 0)
            {
                double implied = f.GramsPerSquareMeter.Value * (f.WidthCm.Value / 100.0) * f.QuantityMeters.Value / 1000.0;
                if (implied > 0) f.WeightConsistency = f.NetKg.Value / implied;
            }
        }

        private static double? FirstNumber(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var m = NumberRx.Match(s);
            if (!m.Success) return null;
            double v;
            if (double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }
    }
}
