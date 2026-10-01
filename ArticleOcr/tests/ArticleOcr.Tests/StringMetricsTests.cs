using ArticleOcr.Guessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArticleOcr.Tests
{
    [TestClass]
    public class StringMetricsTests
    {
        [TestMethod]
        public void IdenticalStringsHaveZeroDistance()
        {
            Assert.AreEqual(0.0, StringMetrics.WeightedEditDistance("RD258150", "RD258150", null), 1e-9);
            Assert.AreEqual(0.0, StringMetrics.WeightedEditDistance("rd258150", "RD258150", null), 1e-9, "case-insensitive");
        }

        [TestMethod]
        public void ConfusableSubstitutionIsCheaperThanUnrelatedOne()
        {
            double oForZero = StringMetrics.WeightedEditDistance("RD25815O", "RD258150", null);
            double wForZero = StringMetrics.WeightedEditDistance("RD25815W", "RD258150", null);
            Assert.IsTrue(oForZero < 0.3, "0/O should be nearly free, was " + oForZero);
            Assert.AreEqual(1.0, wForZero, 1e-9, "0/W is a full substitution");
            Assert.IsTrue(oForZero < wForZero);
        }

        [TestMethod]
        public void DigitForDigitSubstitutionCostsLessThanFullButMoreThanConfusable()
        {
            double d = StringMetrics.WeightedEditDistance("RD258150", "RD258151", null);
            Assert.IsTrue(d > 0.5 && d < 1.0, "was " + d);
        }

        [TestMethod]
        public void NoiseCharactersAreCheapToDelete()
        {
            double d = StringMetrics.WeightedEditDistance("RD.258-150", "RD258150", null);
            Assert.IsTrue(d < 0.7, "two noise deletions should cost < 0.7, was " + d);
        }

        [TestMethod]
        public void TranspositionIsOneEditNotTwo()
        {
            double d = StringMetrics.WeightedEditDistance("RD285150", "RD258150", null);
            Assert.IsTrue(d <= OcrConfusionModel.Default.TransposeCost + 1e-9, "was " + d);
        }

        [TestMethod]
        public void EmptyStringsCost()
        {
            Assert.AreEqual(0.0, StringMetrics.WeightedEditDistance("", "", null), 1e-9);
            Assert.AreEqual(3.0, StringMetrics.WeightedEditDistance("", "ABC", null), 1e-9);
            Assert.AreEqual(3.0, StringMetrics.WeightedEditDistance("ABC", "", null), 1e-9);
        }

        [TestMethod]
        public void JaroWinklerBehaves()
        {
            Assert.AreEqual(1.0, StringMetrics.JaroWinkler("RD258150", "RD258150"), 1e-9);
            Assert.AreEqual(0.0, StringMetrics.JaroWinkler("", "RD258150"), 1e-9);
            double close = StringMetrics.JaroWinkler("RD25815O", "RD258150");
            double far = StringMetrics.JaroWinkler("XYZ", "RD258150");
            Assert.IsTrue(close > 0.9, "was " + close);
            Assert.IsTrue(far < 0.5, "was " + far);
            Assert.AreEqual(0.9611, StringMetrics.JaroWinkler("MARTHA", "MARHTA"), 0.001, "textbook value");
        }

        [TestMethod]
        public void NGramCosineBehaves()
        {
            Assert.AreEqual(1.0, StringMetrics.NGramCosine("RD258150", "RD258150"), 1e-9);
            Assert.IsTrue(StringMetrics.NGramCosine("RD258150", "RD258151") > 0.6);
            Assert.IsTrue(StringMetrics.NGramCosine("RD258150", "ZZZZZZ") < 0.1);
        }

        [TestMethod]
        public void ConfusionModelIsSymmetricAndListsAlternatives()
        {
            var m = OcrConfusionModel.Default;
            Assert.AreEqual(m.Similarity('0', 'O'), m.Similarity('O', '0'), 1e-9);
            Assert.AreEqual(1.0, m.Similarity('A', 'a'), 1e-9);
            var alts = new System.Collections.Generic.List<char>();
            foreach (var kv in m.Alternatives('5')) alts.Add(kv.Key);
            CollectionAssert.Contains(alts, 'S');
            CollectionAssert.Contains(alts, '5');
        }
    }
}
