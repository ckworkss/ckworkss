using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using ArticleOcr.Models;

namespace ArticleOcr.Web
{
    /// <summary>Turns an <see cref="ArticleOcrResult"/> into HTML for the page and into a plain object for JSON.</summary>
    public static class ResultRenderer
    {
        public static string ToHtml(ArticleOcrResult r, bool includeDebug)
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;

            sb.Append("<div class='verdict ").Append(r.IsVerified ? "ok" : (r.ArticleCode == null ? "none" : "guess")).Append("'>");
            if (r.ArticleCode == null)
            {
                sb.Append("<div class='code'>No article code found</div>");
            }
            else
            {
                sb.Append("<div class='label'>").Append(r.IsVerified ? "Article code" : "Best guess").Append("</div>");
                sb.Append("<div class='code'>").Append(E(r.ArticleCode)).Append("</div>");
                sb.Append("<div class='meta'>confidence ").Append((r.Confidence * 100).ToString("0", ci)).Append("% &middot; ")
                  .Append(E(Describe(r.Method))).Append(" &middot; from ").Append(E(Describe(r.Source))).Append("</div>");
            }
            sb.Append("</div>");

            if (r.Candidates.Count > 0)
            {
                sb.Append("<h3>Candidates</h3><table class='grid'><tr><th>Code</th><th>Probability</th><th>Observed</th><th>Distance</th><th>Method</th><th>Source</th><th>Votes</th><th>Pattern</th></tr>");
                foreach (var c in r.Candidates)
                {
                    sb.Append("<tr><td class='mono'>").Append(E(c.Code)).Append("</td><td>")
                      .Append("<div class='bar'><span style='width:").Append((c.Probability * 100).ToString("0", ci)).Append("%'></span></div>")
                      .Append((c.Probability * 100).ToString("0.0", ci)).Append("%</td><td class='mono'>").Append(E(c.Observed))
                      .Append("</td><td>").Append(c.Distance.ToString("0.00", ci)).Append("</td><td>").Append(E(Describe(c.Method)))
                      .Append("</td><td>").Append(E(Describe(c.Source))).Append("</td><td>").Append(c.Votes)
                      .Append("</td><td>").Append(c.MatchesGrammar ? "yes" : "no").Append("</td></tr>");
                }
                sb.Append("</table>");
            }

            if (r.Fields != null)
            {
                var d = r.Fields.ToDictionary();
                if (d.Count > 0)
                {
                    sb.Append("<h3>Label fields</h3><table class='grid'>");
                    foreach (var kv in d)
                        sb.Append("<tr><th>").Append(E(kv.Key)).Append("</th><td class='mono'>").Append(E(kv.Value)).Append("</td></tr>");
                    if (r.Fields.WeightConsistency.HasValue)
                    {
                        var wc = r.Fields.WeightConsistency.Value;
                        sb.Append("<tr><th>Weight check</th><td>net / (gsm x width x length) = ").Append(wc.ToString("0.00", ci))
                          .Append(Math.Abs(wc - 1) < 0.15 ? " (numbers agree)" : " (numbers disagree: re-check width / qty / net)").Append("</td></tr>");
                    }
                    sb.Append("</table>");
                }
            }

            if (includeDebug)
            {
                sb.Append("<h3>OCR text</h3><pre>").Append(E(r.RawText)).Append("</pre>");
                sb.Append("<h3>Diagnostics</h3><ul class='diag'>");
                sb.Append("<li>elapsed ").Append((int)r.Elapsed.TotalMilliseconds).Append(" ms");
                if (r.BestVariant != null) sb.Append(", best variant <b>").Append(E(r.BestVariant)).Append("</b>, mean OCR confidence ").Append((r.OcrConfidence * 100).ToString("0", ci)).Append("%");
                sb.Append("</li>");
                foreach (var line in r.Diagnostics) sb.Append("<li>").Append(E(line)).Append("</li>");
                sb.Append("</ul>");
            }
            return sb.ToString();
        }

        public static Dictionary<string, object> ToJsonObject(ArticleOcrResult r, bool includeDebug)
        {
            var o = new Dictionary<string, object>
            {
                { "articleCode", r.ArticleCode },
                { "confidence", Math.Round(r.Confidence, 4) },
                { "verified", r.IsVerified },
                { "method", r.Method.ToString() },
                { "source", r.Source.ToString() },
                { "candidates", r.Candidates.Select(c => new Dictionary<string, object>
                    {
                        { "code", c.Code },
                        { "probability", Math.Round(c.Probability, 4) },
                        { "observed", c.Observed },
                        { "distance", Math.Round(c.Distance, 3) },
                        { "jaroWinkler", Math.Round(c.JaroWinkler, 3) },
                        { "ocrConfidence", Math.Round(c.OcrConfidence, 3) },
                        { "method", c.Method.ToString() },
                        { "source", c.Source.ToString() },
                        { "votes", c.Votes },
                        { "matchesGrammar", c.MatchesGrammar },
                        { "notes", c.Notes }
                    }).ToList() },
                { "fields", r.Fields != null ? r.Fields.ToDictionary() : new Dictionary<string, string>() },
                { "weightConsistency", r.Fields != null ? r.Fields.WeightConsistency : null },
                { "elapsedMs", (int)r.Elapsed.TotalMilliseconds }
            };
            if (includeDebug)
            {
                o["rawText"] = r.RawText;
                o["bestVariant"] = r.BestVariant;
                o["ocrConfidence"] = Math.Round(r.OcrConfidence, 3);
                o["diagnostics"] = r.Diagnostics;
            }
            return o;
        }

        public static string Describe(CandidateMethod m)
        {
            switch (m)
            {
                case CandidateMethod.Exact: return "exact match in master list";
                case CandidateMethod.MasterList: return "matched to master list";
                case CandidateMethod.GrammarRepair: return "repaired to fit the article pattern";
                case CandidateMethod.GrammarOnly: return "fits the pattern (not in master list)";
                default: return "unverified";
            }
        }

        public static string Describe(CandidateSource s)
        {
            switch (s)
            {
                case CandidateSource.ArticleField: return "Article line";
                case CandidateSource.QualityField: return "Quality line";
                case CandidateSource.RawToken: return "other text on the label";
                default: return "typed text";
            }
        }

        private static string E(string s)
        {
            return HttpUtility.HtmlEncode(s ?? string.Empty);
        }
    }
}
