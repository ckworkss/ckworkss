using System;
using System.Collections.Generic;
using System.Linq;

namespace ArticleOcr.Guessing
{
    /// <summary>
    /// Expands a simple regular expression into representative strings ("exemplars") so the grammar's
    /// positional class profile can be learned from the patterns themselves.
    /// <para>
    /// Supported subset: character classes (<c>[A-Z]</c>, <c>[0-9]</c>, <c>\d</c>, <c>\w</c>), literals,
    /// groups with alternation, and the quantifiers <c>{n}</c>, <c>{a,b}</c>, <c>?</c>, <c>+</c>, <c>*</c>.
    /// Ranged quantifiers produce the minimum and maximum repetition only. Anything unsupported yields nothing.
    /// </para>
    /// </summary>
    public static class PatternExemplars
    {
        private const int MaxExemplarsPerPattern = 64;

        public static IEnumerable<string> Expand(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return Enumerable.Empty<string>();
            var p = pattern.Trim();
            if (p.StartsWith("^")) p = p.Substring(1);
            if (p.EndsWith("$") && !p.EndsWith(@"\$")) p = p.Substring(0, p.Length - 1);
            try
            {
                int pos = 0;
                var result = ParseAlternation(p, ref pos);
                if (pos != p.Length) return Enumerable.Empty<string>();
                return result.Where(s => s.Length > 0).Distinct().Take(MaxExemplarsPerPattern).ToList();
            }
            catch (FormatException)
            {
                return Enumerable.Empty<string>();
            }
        }

        private static List<string> ParseAlternation(string p, ref int pos)
        {
            var all = new List<string>();
            all.AddRange(ParseSequence(p, ref pos));
            while (pos < p.Length && p[pos] == '|')
            {
                pos++;
                all.AddRange(ParseSequence(p, ref pos));
            }
            return all;
        }

        private static List<string> ParseSequence(string p, ref int pos)
        {
            var current = new List<string> { string.Empty };
            while (pos < p.Length && p[pos] != '|' && p[pos] != ')')
            {
                var atom = ParseAtom(p, ref pos);
                int min, max;
                ParseQuantifier(p, ref pos, out min, out max);
                var reps = new List<string>();
                foreach (var count in new[] { min, max }.Distinct())
                    reps.AddRange(Repeat(atom, count));
                current = Combine(current, reps.Distinct().ToList());
                if (current.Count > MaxExemplarsPerPattern) current = current.Take(MaxExemplarsPerPattern).ToList();
            }
            return current;
        }

        private static List<string> ParseAtom(string p, ref int pos)
        {
            char c = p[pos];
            if (c == '(')
            {
                pos++;
                if (pos + 1 < p.Length && p[pos] == '?' && p[pos + 1] == ':') pos += 2;   // non-capturing
                var inner = ParseAlternation(p, ref pos);
                if (pos >= p.Length || p[pos] != ')') throw new FormatException("unbalanced group");
                pos++;
                return inner;
            }
            if (c == '[')
            {
                int end = p.IndexOf(']', pos + 1);
                if (end < 0) throw new FormatException("unbalanced class");
                var body = p.Substring(pos + 1, end - pos - 1);
                pos = end + 1;
                return new List<string> { ClassRepresentative(body).ToString() };
            }
            if (c == '\\')
            {
                if (pos + 1 >= p.Length) throw new FormatException("dangling escape");
                char e = p[pos + 1];
                pos += 2;
                switch (e)
                {
                    case 'd': return new List<string> { "0" };
                    case 'w': return new List<string> { "A" };
                    case 's': return new List<string> { " " };
                    default: return new List<string> { e.ToString() };
                }
            }
            if (c == '.') { pos++; return new List<string> { "A" }; }
            if ("*+?{}".IndexOf(c) >= 0) throw new FormatException("unexpected quantifier");
            pos++;
            return new List<string> { c.ToString() };
        }

        private static char ClassRepresentative(string body)
        {
            bool neg = body.StartsWith("^");
            if (neg) return 'A';
            var b = body.ToUpperInvariant();
            if (b.Contains("A-Z") || b.Contains(@"\W") || b.Any(char.IsLetter) && !b.Contains("0-9")) return 'A';
            if (b.Contains("0-9") || b.Contains(@"\D") || b.Any(char.IsDigit)) return '0';
            var first = body.FirstOrDefault(ch => ch != '\\');
            return first == '\0' ? 'A' : first;
        }

        private static void ParseQuantifier(string p, ref int pos, out int min, out int max)
        {
            min = max = 1;
            if (pos >= p.Length) return;
            char c = p[pos];
            if (c == '?') { pos++; min = 0; max = 1; }
            else if (c == '*') { pos++; min = 0; max = 2; }
            else if (c == '+') { pos++; min = 1; max = 2; }
            else if (c == '{')
            {
                int end = p.IndexOf('}', pos);
                if (end < 0) throw new FormatException("unbalanced quantifier");
                var body = p.Substring(pos + 1, end - pos - 1);
                pos = end + 1;
                var parts = body.Split(',');
                min = int.Parse(parts[0].Trim());
                max = parts.Length > 1 ? (parts[1].Trim().Length == 0 ? min + 1 : int.Parse(parts[1].Trim())) : min;
            }
            else return;
            if (pos < p.Length && p[pos] == '?') pos++;   // lazy modifier
            if (max < min) max = min;
        }

        private static List<string> Repeat(List<string> atom, int count)
        {
            var result = new List<string> { string.Empty };
            for (int i = 0; i < count; i++) result = Combine(result, atom);
            return result;
        }

        private static List<string> Combine(List<string> prefixes, List<string> suffixes)
        {
            var result = new List<string>();
            foreach (var a in prefixes)
                foreach (var b in suffixes)
                {
                    result.Add(a + b);
                    if (result.Count >= MaxExemplarsPerPattern) return result;
                }
            return result;
        }
    }
}
