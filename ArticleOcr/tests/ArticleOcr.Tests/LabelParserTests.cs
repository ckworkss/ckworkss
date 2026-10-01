using ArticleOcr.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArticleOcr.Tests
{
    [TestClass]
    public class LabelParserTests
    {
        private const string Label1 =
            "BEST PACIFIC\n" +
            "Quality : TE181065FX2-A2\n" +
            "Color : EPB80772HPM\n" +
            "Customer : N060\n" +
            "Batch : V23016221\n" +
            "Roll : 11\n" +
            "423061001874\n" +
            "Width : 158cm / 62.20\" 2023-06-11\n" +
            "QTY : 48.00Y Weight: 240 g/m2\n" +
            "Gross : 18.00KG Free:\n" +
            "PO. No. : TMKF-23-03459-811878 Net: 17.00KG\n" +
            "Article : RD258150\n" +
            "Color : Mocha Taffy CSI 589\n" +
            "Remark :";

        [TestMethod]
        public void ParsesCleanLabel()
        {
            var f = new LabelParser().Parse(Label1);
            Assert.AreEqual("TE181065FX2-A2", f.Quality);
            Assert.AreEqual("EPB80772HPM", f.ColorCode);
            Assert.AreEqual("N060", f.Customer);
            Assert.AreEqual("V23016221", f.Batch);
            Assert.AreEqual("11", f.Roll);
            Assert.AreEqual("423061001874", f.Barcode);
            Assert.AreEqual("158cm / 62.20\"", f.Width);
            Assert.AreEqual("48.00Y", f.Quantity);
            Assert.AreEqual("18.00KG", f.Gross);
            Assert.AreEqual("17.00KG", f.Net);
            Assert.AreEqual("240 g/m2", f.Weight);
            Assert.AreEqual("TMKF-23-03459-811878", f.PoNo);
            Assert.AreEqual("RD258150", f.Article);
            Assert.AreEqual("Mocha Taffy CSI 589", f.ColorName);
            Assert.AreEqual("2023-06-11", f.Date);
            Assert.IsNull(f.Remark);
        }

        [TestMethod]
        public void NumericFieldsAndWeightConsistency()
        {
            var f = new LabelParser().Parse(Label1);
            Assert.AreEqual(158, f.WidthCm.Value, 1e-9);
            Assert.AreEqual(48 * 0.9144, f.QuantityMeters.Value, 1e-6);
            Assert.AreEqual(18, f.GrossKg.Value, 1e-9);
            Assert.AreEqual(17, f.NetKg.Value, 1e-9);
            Assert.AreEqual(240, f.GramsPerSquareMeter.Value, 1e-9);
            // 240 g/m2 * 1.58 m * 43.89 m = 16.64 kg vs 17.00 kg stated
            Assert.AreEqual(1.02, f.WeightConsistency.Value, 0.01);
        }

        [TestMethod]
        public void FuzzyKeysAreRecognised()
        {
            var f = new LabelParser().Parse("Qua1ity : EA511872FX30\nCo1or : WPZ2/877\nCustomer : Z0F3G\nBatch : VN25005670\nArtic1e : EA511?72FX30\nCo1or : TRANG\nMADE IN VIETNAM");
            Assert.AreEqual("EA511872FX30", f.Quality);
            Assert.AreEqual("WPZ2/877", f.ColorCode);
            Assert.AreEqual("VN25005670", f.Batch);
            Assert.AreEqual("EA511?72FX30", f.Article);
            Assert.AreEqual("TRANG", f.ColorName);
        }

        [TestMethod]
        public void ShortKeysMustMatchExactly()
        {
            // "NEW" must not be mistaken for "NET"
            var f = new LabelParser().Parse("NEW 17.00KG");
            Assert.IsNull(f.Net);
            Assert.AreEqual("17.00KG", new LabelParser().Parse("Net 17.00KG").Net);
        }

        [TestMethod]
        public void MetersQuantityIsNotConverted()
        {
            var f = new LabelParser().Parse("QTY : 13.92M");
            Assert.AreEqual(13.92, f.QuantityMeters.Value, 1e-9);
        }

        [TestMethod]
        public void BpOrderNoAndSingleColorName()
        {
            var f = new LabelParser().Parse("BP Order No. : VNP162024112102730\nColor : Black 0813");
            Assert.AreEqual("VNP162024112102730", f.BpOrderNo);
            Assert.IsNull(f.ColorCode, "a colour line whose first word has no digits is a name");
            Assert.AreEqual("Black 0813", f.ColorName);

            var g = new LabelParser().Parse("Color : V01A001A Black 0813");
            Assert.AreEqual("V01A001A", g.ColorCode);
            Assert.AreEqual("Black 0813", g.ColorName);
        }

        [TestMethod]
        public void CleanCodeNormalises()
        {
            Assert.AreEqual("RD258150", LabelParser.CleanCode(" : rd 258150. "));
            Assert.IsNull(LabelParser.CleanCode("  "));
            Assert.IsNull(LabelParser.CleanCode(null));
        }

        [TestMethod]
        public void EmptyInputGivesEmptyFields()
        {
            var f = new LabelParser().Parse("");
            Assert.AreEqual(0, f.ToDictionary().Count);
            Assert.AreEqual(0, new LabelParser().Parse((string)null).ToDictionary().Count);
        }
    }
}
