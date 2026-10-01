using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ArticleOcr.Guessing
{
    /// <summary>A known article code with an optional description and a frequency used as its prior.</summary>
    public sealed class ArticleEntry
    {
        public string Code { get; set; }
        public string Description { get; set; }
        /// <summary>How often this code is seen (e.g. rolls received). Drives the Bayesian prior.</summary>
        public double Count { get; set; } = 1;
    }

    /// <summary>
    /// The list of article codes the system knows about. Load from CSV (<c>Code,Description,Count</c>),
    /// from a database query, or build in memory.
    /// </summary>
    public sealed class ArticleMasterList
    {
        private readonly Dictionary<string, ArticleEntry> _entries = new Dictionary<string, ArticleEntry>(StringComparer.OrdinalIgnoreCase);
        private double _total;

        public int Count { get { return _entries.Count; } }
        public IEnumerable<ArticleEntry> Entries { get { return _entries.Values; } }
        public IEnumerable<string> Codes { get { return _entries.Keys; } }

        public static string Normalize(string code)
        {
            if (code == null) return null;
            return new string(code.Trim().ToUpperInvariant().Where(c => !char.IsWhiteSpace(c)).ToArray());
        }

        public void Add(string code, string description = null, double count = 1)
        {
            code = Normalize(code);
            if (string.IsNullOrEmpty(code)) return;
            ArticleEntry e;
            if (_entries.TryGetValue(code, out e))
            {
                e.Count += count;
                if (string.IsNullOrEmpty(e.Description)) e.Description = description;
            }
            else
            {
                _entries[code] = new ArticleEntry { Code = code, Description = description, Count = count };
            }
            _total += count;
        }

        public bool Contains(string code)
        {
            code = Normalize(code);
            return !string.IsNullOrEmpty(code) && _entries.ContainsKey(code);
        }

        public ArticleEntry Get(string code)
        {
            ArticleEntry e;
            return _entries.TryGetValue(Normalize(code) ?? string.Empty, out e) ? e : null;
        }

        /// <summary>Smoothed prior probability of a code: (count + 1) / (total + N).</summary>
        public double Prior(string code)
        {
            if (_entries.Count == 0) return 1.0;
            var e = Get(code);
            double c = e == null ? 0 : e.Count;
            return (c + 1.0) / (_total + _entries.Count);
        }

        /// <summary>Load <c>Code,Description,Count</c> CSV. Header row optional, '#' comments allowed.</summary>
        public static ArticleMasterList LoadCsv(string path)
        {
            var list = new ArticleMasterList();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return list;
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split(',', ';', '\t');
                var code = parts[0].Trim().Trim('"');
                if (string.Equals(code, "code", StringComparison.OrdinalIgnoreCase)) continue;
                string desc = parts.Length > 1 ? parts[1].Trim().Trim('"') : null;
                double count = 1;
                if (parts.Length > 2) double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out count);
                if (count <= 0) count = 1;
                list.Add(code, desc, count);
            }
            return list;
        }
    }
}
