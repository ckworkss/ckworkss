using System.IO;
using System.Linq;
using ArticleOcr.Guessing;
using ArticleOcr.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArticleOcr.Tests
{
    [TestClass]
    public class ArticleGuesserTests
    {
        private static ArticleMasterList Master()
        {
            var m = new ArticleMasterList();
            m.Add("RD258150", "Mocha Taffy", 40);
            m.Add("RD258151", null, 5);
            m.Add("RD258160", null, 5);
            m.Add("EA511872FX30", null, 20);
            m.Add("EA511672FX30", null, 5);
            m.Add("TE181065FX2-A2", null, 10);
            return m;
        }

        private static ArticleGuesser Guesser()
        {
            return new ArticleGuesser(Master(), new ArticleCodeGrammar());
        }

        [TestMethod]
        public void ExactCodeIsVerified()
        {
            var g = Guesser();
            var r = g.Guess("RD258150");
            Assert.AreEqual("RD258150", r[0].Code);
            Assert.AreEqual(CandidateMethod.Exact, r[0].Method);
            Assert.IsTrue(r[0].Probability > 0.8);
            Assert.IsTrue(g.IsVerified(r));
        }

        [TestMethod]
        public void ConfusedCharactersAreRecovered()
        {
            var g = Guesser();
            foreach (var noisy in new[] { "RD25815O", "R0258I5O", "RD 258 150", "rd258150", "RD.258150" })
            {
                var r = g.Guess(noisy);
                Assert.AreEqual("RD258150", r[0].Code, noisy);
                Assert.IsTrue(g.IsVerified(r), noisy + " should be verified, p=" + r[0].Probability);
            }
        }

        [TestMethod]
        public void AmbiguousObservationIsNotVerified()
        {
            var g = Guesser();
            var r = g.Guess("EA511?72FX30");   // 8 or 6: both known codes
            var top2 = r.Take(2).Select(c => c.Code).ToList();
            CollectionAssert.Contains(top2, "EA511872FX30");
            CollectionAssert.Contains(top2, "EA511672FX30");
            Assert.IsFalse(g.IsVerified(r));
        }

        [TestMethod]
        public void UnreadableCharacterIsAGuessEvenWhenPriorPrefersOneCode()
        {
            // RD258150 (count 40) vs RD258151 (count 5): "RD25815?" leans to the frequent one but one character is unknown.
            var g = Guesser();
            var r = g.Guess("RD25815?");
            Assert.AreEqual("RD258150", r[0].Code);
            Assert.IsTrue(r[0].Distance >= 1.0);
            Assert.IsFalse(g.IsVerified(r), "a fully unreadable character must not be reported as verified");
        }

        [TestMethod]
        public void PriorBreaksTies()
        {
            // RD258150 has count 40, RD258151 count 5: an observation equally far from both prefers the frequent one.
            var g = Guesser();
            var r = g.Guess("RD25815");
            Assert.AreEqual("RD258150", r[0].Code);
        }

        [TestMethod]
        public void MultipleObservationsVote()
        {
            var g = Guesser();
            var r = g.Guess(new[]
            {
                new Observation { Text = "RD25815O", Source = CandidateSource.ArticleField, Confidence = 0.5 },
                new Observation { Text = "RD258150", Source = CandidateSource.RawToken, Confidence = 0.8 },
                new Observation { Text = "TE181065FX2-A2", Source = CandidateSource.QualityField, Confidence = 0.9 }
            });
            Assert.AreEqual("RD258150", r[0].Code);
            Assert.IsTrue(r[0].Votes >= 2, "two observations should merge into one candidate");
            Assert.IsTrue(r.Any(c => c.Code == "TE181065FX2-A2"));
        }

        [TestMethod]
        public void UnknownCodeThatFitsGrammarIsReportedButNotVerifiedWhenListRequired()
        {
            var g = Guesser();
            g.RequireMasterListForVerification = true;
            var r = g.Guess("ZZ999999");
            Assert.AreEqual("ZZ999999", r[0].Code);
            Assert.AreEqual(CandidateMethod.GrammarOnly, r[0].Method);
            Assert.IsFalse(g.IsVerified(r));
            g.RequireMasterListForVerification = false;
            Assert.IsTrue(g.IsVerified(r));
        }

        [TestMethod]
        public void GarbageIsUnverified()
        {
            var g = Guesser();
            var r = g.Guess("Mocha");
            Assert.IsFalse(g.IsVerified(r));
            Assert.IsTrue(r.All(c => c.Probability < 0.6));
            Assert.AreEqual(0, g.Guess("").Count);
            Assert.AreEqual(0, g.Guess((string)null).Count);
        }

        [TestMethod]
        public void WithoutMasterListGrammarRepairStillWorks()
        {
            var g = new ArticleGuesser(new ArticleMasterList(), new ArticleCodeGrammar());
            var r = g.Guess("EA5118Z2FX3O");
            Assert.AreEqual("EA511822FX30", r[0].Code);
            Assert.AreEqual(CandidateMethod.GrammarRepair, r[0].Method);
            Assert.IsTrue(r[0].MatchesGrammar);
        }

        [TestMethod]
        public void ProbabilitiesSumToAtMostOne()
        {
            var r = Guesser().Guess("RD25815O");
            Assert.IsTrue(r.Sum(c => c.Probability) <= 1.0 + 1e-9);
            Assert.IsTrue(r.Sum(c => c.Probability) > 0.9);
        }

        [TestMethod]
        public void MasterListCsvLoads()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "# comment\nCode,Description,Count\nRD258150,Mocha,40\n\"EA511872FX30\",\"X\",3\nBAD\n");
                var m = ArticleMasterList.LoadCsv(path);
                Assert.AreEqual(3, m.Count);
                Assert.AreEqual(40, m.Get("rd258150").Count);
                Assert.AreEqual("Mocha", m.Get("RD258150").Description);
                Assert.IsTrue(m.Prior("RD258150") > m.Prior("BAD"));
                Assert.AreEqual(0, ArticleMasterList.LoadCsv(path + ".missing").Count);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
