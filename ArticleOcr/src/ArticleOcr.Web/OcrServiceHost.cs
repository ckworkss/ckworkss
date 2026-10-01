using System;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Web.Hosting;

namespace ArticleOcr.Web
{
    /// <summary>
    /// Application-wide singleton for <see cref="ArticleOcrService"/> configured from Web.config appSettings.
    /// The service owns the Tesseract engine pool, so there must be exactly one per process.
    /// </summary>
    public static class OcrServiceHost
    {
        private static readonly object Sync = new object();
        private static ArticleOcrService _service;

        public static ArticleOcrService Service
        {
            get
            {
                if (_service != null) return _service;
                lock (Sync)
                {
                    if (_service == null) _service = new ArticleOcrService(BuildOptions());
                    return _service;
                }
            }
        }

        public static void Warmup()
        {
            var s = Service;
            // run a trivial guess so the master list and grammar are loaded
            s.GuessFromText("WARMUP");
        }

        public static void Shutdown()
        {
            lock (Sync)
            {
                if (_service != null) _service.Dispose();
                _service = null;
            }
        }

        public static ArticleOcrOptions BuildOptions()
        {
            var o = new ArticleOcrOptions
            {
                TessDataPath = MapPath(Setting("Ocr:TessDataPath", "~/tessdata")),
                Language = Setting("Ocr:Language", "eng"),
                MasterListPath = MapPath(Setting("Ocr:MasterListPath", null)),
                MaxImageDimension = Int("Ocr:MaxImageDimension", 2200),
                MinImageDimension = Int("Ocr:MinImageDimension", 1400),
                RotationFallbackConfidence = Dbl("Ocr:RotationFallbackConfidence", 0.35),
                MaxEngines = Int("Ocr:MaxEngines", 0),
                Lambda = Dbl("Ocr:Lambda", 2.0),
                VerifiedThreshold = Dbl("Ocr:VerifiedThreshold", 0.60),
                VerifiedMargin = Dbl("Ocr:VerifiedMargin", 0.25),
                RequireMasterListForVerification = string.Equals(Setting("Ocr:RequireMasterListForVerification", "false"), "true", StringComparison.OrdinalIgnoreCase)
            };
            var patterns = Setting("Ocr:ArticlePatterns", null);
            if (!string.IsNullOrWhiteSpace(patterns))
                o.ArticlePatterns = patterns.Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            return o;
        }

        private static string Setting(string key, string fallback)
        {
            var v = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();
        }

        private static int Int(string key, int fallback)
        {
            int v;
            return int.TryParse(Setting(key, null), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        private static double Dbl(string key, double fallback)
        {
            double v;
            return double.TryParse(Setting(key, null), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        private static string MapPath(string virtualOrPhysical)
        {
            if (string.IsNullOrEmpty(virtualOrPhysical)) return null;
            if (virtualOrPhysical.StartsWith("~") && HostingEnvironment.IsHosted) return HostingEnvironment.MapPath(virtualOrPhysical);
            return virtualOrPhysical;
        }
    }
}
