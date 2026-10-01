using System.Linq;
using ArticleOcr.Guessing;
using ArticleOcr.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArticleOcr.Tests
{
    [TestClass]
    public class GrammarTests
    {
        [TestMethod]
        public void DefaultPatternsAcceptLabelCodes()
        {
            var g = new ArticleCodeGrammar();
            Assert.IsTrue(g.IsValid("RD258150"));
            Assert.IsTrue(g.IsValid("EA511872FX30"));
            Assert.IsTrue(g.IsValid("TE181065FX2-A2"));
            Assert.IsFalse(g.IsValid("MOCHA"));
            Assert.IsFalse(g.IsValid("rd258150"), "codes are upper case");
            Assert.IsFalse(g.IsValid(""));
        }

        [TestMethod]
        public void ExemplarsAreGeneratedFromPatterns()
        {
            var ex = PatternExemplars.Expand(@"^[A-Z]{2}\d{6}[A-Z]{1,2}\d{1,3}-[A-Z]\d{1,2}$").ToList();
            Assert.IsTrue(ex.Count > 0);
            CollectionAssert.Contains(ex, "AA000000A0-A0");
            CollectionAssert.Contains(ex, "AA000000AA000-A00");
            Assert.IsTrue(ex.All(e => new ArticleCodeGrammar().IsValid(e)), "every exemplar must satisfy its own pattern");

            CollectionAssert.AreEquivalent(new[] { "AB", "AC" }, PatternExemplars.Expand("^A(B|C)$").ToList());
            CollectionAssert.AreEquivalent(new[] { "A", "AB" }, PatternExemplars.Expand("^AB?$").ToList());
            Assert.AreEqual(0, PatternExemplars.Expand(@"^(?<x>A)\k<x>$").Count(), "unsupported syntax yields nothing instead of throwing");
        }

        [TestMethod]
        public void ProfileIsLearnedFromPatternsWithoutMasterList()
        {
            var g = new ArticleCodeGrammar();
            Assert.IsTrue(g.ClassPrior(0, ArticleCodeGrammar.ClassLetter) > 0.8, "first position is a letter");
            Assert.IsTrue(g.ClassPrior(3, ArticleCodeGrammar.ClassDigit) > 0.8, "fourth position is a digit");
            Assert.IsTrue(g.ShapeLogLikelihood("RD258150") > g.ShapeLogLikelihood("12345678"));
        }

        [TestMethod]
        public void ProfileLearnsFromMasterList()
        {
            var g = new ArticleCodeGrammar(new[] { "^.*$" });
            g.Learn(new[] { "AB123456", "CD234567", "EF345678", "GH456789", "JK567890" });
            Assert.IsTrue(g.ClassPrior(1, ArticleCodeGrammar.ClassLetter) > 0.8);
            Assert.IsTrue(g.ClassPrior(2, ArticleCodeGrammar.ClassDigit) > 0.8);
            Assert.AreEqual(8, g.MinLength);
            Assert.AreEqual(8, g.MaxLength);
            Assert.IsTrue(g.ClassTransition(ArticleCodeGrammar.ClassDigit, ArticleCodeGrammar.ClassDigit) > g.ClassTransition(ArticleCodeGrammar.ClassDigit, ArticleCodeGrammar.ClassLetter));
        }

        [TestMethod]
        public void PlausibleTokenFilter()
        {
            var g = new ArticleCodeGrammar();
            Assert.IsTrue(g.IsPlausibleToken("RD258150"));
            Assert.IsTrue(g.IsPlausibleToken("EA511?72FX30"));
            Assert.IsFalse(g.IsPlausibleToken("MOCHA"));
            Assert.IsFalse(g.IsPlausibleToken("48.00Y"));
            Assert.IsFalse(g.IsPlausibleToken("423061001874"), "pure digits are a barcode, not an article");
        }

        [TestMethod]
        public void RepairFixesConfusablesToFitGrammar()
        {
            var repair = new GrammarRepair(new ArticleCodeGrammar(), OcrConfusionModel.Default);
            var r = repair.Repair("EA5118Z2FX3O", 3);
            Assert.IsTrue(r.Count > 0);
            Assert.IsTrue(r[0].MatchesGrammar);
            Assert.AreEqual("EA511822FX30", r[0].Code);

            var dash = repair.Repair("TE1B1O65FX2-A2", 3);
            Assert.AreEqual("TE181065FX2-A2", dash[0].Code, "hyphen is kept, B->8 and O->0 repaired");
        }

        [TestMethod]
        public void RepairDropsNoiseCharacters()
        {
            var repair = new GrammarRepair(new ArticleCodeGrammar(), OcrConfusionModel.Default);
            var r = repair.Repair("RD.258150'", 3);
            Assert.AreEqual("RD258150", r[0].Code);
            Assert.IsTrue(r[0].MatchesGrammar);
        }

        [TestMethod]
        public void RepairUsesEngineAlternatives()
        {
            var repair = new GrammarRepair(new ArticleCodeGrammar(), OcrConfusionModel.Default);
            // The engine saw '8' but was unsure and listed '6' as a strong alternative.
            var symbols = "RD258150".Select(c => new OcrSymbol { Char = c, Confidence = 0.95 }).ToList();
            symbols[4].Confidence = 0.40;
            symbols[4].Choices.Add(new System.Collections.Generic.KeyValuePair<char, double>('8', 0.40));
            symbols[4].Choices.Add(new System.Collections.Generic.KeyValuePair<char, double>('6', 0.39));
            var r = repair.Repair(symbols, 5);
            var codes = r.Select(x => x.Code).ToList();
            CollectionAssert.Contains(codes, "RD258150");
            CollectionAssert.Contains(codes, "RD256150");
        }

        [TestMethod]
        public void RepairReturnsSomethingForHopelessInput()
        {
            var repair = new GrammarRepair(new ArticleCodeGrammar(), OcrConfusionModel.Default);
            var r = repair.Repair("###", 3);
            Assert.IsTrue(r.Count >= 0);   // must not throw
            Assert.AreEqual(0, repair.Repair("", 3).Count);
        }
    }
}
