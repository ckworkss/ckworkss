using System.Collections.Generic;
using System.IO;
using ArticleOcr.Imaging;
using ArticleOcr.Models;
using ArticleOcr.Recognition;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArticleOcr.Tests
{
    /// <summary>End-to-end pipeline tests with a fake OCR engine (no Tesseract needed).</summary>
    [TestClass]
    public class ServiceTests
    {
        private sealed class FakeRecognizer : ITextRecognizer
        {
            private readonly Dictionary<string, string> _byVariant;
            public FakeRecognizer(Dictionary<string, string> byVariant) { _byVariant = byVariant; }

            public OcrPassResult Recognize(GrayImage image, string variantName)
            {
                string text;
                if (!_byVariant.TryGetValue(variantName, out text)) text = "";
                var pass = new OcrPassResult { Variant = variantName, Text = text, MeanConfidence = text.Length == 0 ? 0 : 0.7 };
                int line = 0;
                foreach (var l in text.Split('\n'))
                {
                    foreach (var w in l.Split(' '))
                        if (w.Length > 0) pass.Words.Add(new OcrWord { Text = w, Confidence = 0.7, Line = line });
                    line++;
                }
                return pass;
            }

            public void Dispose() { }
        }

        private static string MasterCsv()
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, "Code,Description,Count\nRD258150,Mocha,40\nEA511872FX30,,10\nEA511672FX30,,2\nTE181065FX2-A2,,5\n");
            return path;
        }

        private static GrayImage Blank()
        {
            var img = new GrayImage(300, 200);
            for (int i = 0; i < img.Data.Length; i++) img.Data[i] = 255;
            return img;
        }

        [TestMethod]
        public void ReadsArticleDirectlyFromLabel()
        {
            var csv = MasterCsv();
            try
            {
                var fake = new FakeRecognizer(new Dictionary<string, string>
                {
                    { "gray", "Quality : TE181065FX2-A2\nBatch : V23016221\nArticle : RD25815O\nColor : Mocha Taffy" },
                    { "sauvola", "Quality : TE181065FX2-A2\nArticle : RD258150" },
                    { "otsu", "" }
                });
                using (var svc = new ArticleOcrService(new ArticleOcrOptions { MasterListPath = csv }, fake))
                {
                    var r = svc.Process(Blank());
                    Assert.AreEqual("RD258150", r.ArticleCode);
                    Assert.IsTrue(r.IsVerified, "p=" + r.Confidence);
                    Assert.AreEqual(CandidateSource.ArticleField, r.Source);
                    Assert.AreEqual("TE181065FX2-A2", r.Fields.Quality);
                    Assert.AreEqual("V23016221", r.Fields.Batch, "fields are merged across passes");
                    Assert.AreEqual(3, r.Passes.Count);
                }
            }
            finally { File.Delete(csv); }
        }

        [TestMethod]
        public void FallsBackToQualityLineWhenArticleUnreadable()
        {
            var csv = MasterCsv();
            try
            {
                var fake = new FakeRecognizer(new Dictionary<string, string>
                {
                    { "gray", "Quality : EA511872FX30\nColor : WPZ2/877\nArticle : ####\nColor : TRANG" },
                    { "sauvola", "Qua1ity : EA5118?2FX30" },
                    { "otsu", "" }
                });
                using (var svc = new ArticleOcrService(new ArticleOcrOptions { MasterListPath = csv }, fake))
                {
                    var r = svc.Process(Blank());
                    Assert.AreEqual("EA511872FX30", r.ArticleCode);
                    Assert.AreEqual(CandidateSource.QualityField, r.Source);
                }
            }
            finally { File.Delete(csv); }
        }

        [TestMethod]
        public void TriesQuadrantRotationsWhenEverythingIsWeak()
        {
            var fake = new FakeRecognizer(new Dictionary<string, string>
            {
                { "gray", "" }, { "sauvola", "" }, { "otsu", "" },
                { "gray-rot90", "Article : RD258150" }
            });
            using (var svc = new ArticleOcrService(new ArticleOcrOptions(), fake))
            {
                var r = svc.Process(Blank());
                Assert.AreEqual("RD258150", r.ArticleCode);
                Assert.AreEqual("gray-rot90", r.BestVariant);
                Assert.IsTrue(r.Passes.Count >= 4);
            }
        }

        [TestMethod]
        public void GuessFromTextWorksWithoutImage()
        {
            var csv = MasterCsv();
            try
            {
                using (var svc = new ArticleOcrService(new ArticleOcrOptions { MasterListPath = csv }, new FakeRecognizer(new Dictionary<string, string>())))
                {
                    var r = svc.GuessFromText("R0258I5O");
                    Assert.AreEqual("RD258150", r.ArticleCode);
                    Assert.IsTrue(r.IsVerified);

                    var whole = svc.GuessFromText("Quality : TE181065FX2-A2\nArticle : RD258150");
                    Assert.AreEqual("RD258150", whole.ArticleCode);
                    Assert.AreEqual("TE181065FX2-A2", whole.Fields.Quality);

                    Assert.IsNull(svc.GuessFromText("   ").ArticleCode);
                }
            }
            finally { File.Delete(csv); }
        }
    }
}
