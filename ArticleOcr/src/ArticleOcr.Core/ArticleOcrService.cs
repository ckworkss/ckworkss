using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ArticleOcr.Guessing;
using ArticleOcr.Imaging;
using ArticleOcr.Models;
using ArticleOcr.Parsing;
using ArticleOcr.Recognition;

namespace ArticleOcr
{
    /// <summary>
    /// End-to-end pipeline: image -> preprocessing variants -> OCR passes -> label parsing -> candidate
    /// observations -> Bayesian article-code guess.
    /// Create one instance per application (it owns the Tesseract engine pool) and call <see cref="Process"/> per image.
    /// </summary>
    public sealed class ArticleOcrService : IDisposable
    {
        private readonly ArticleOcrOptions _options;
        private readonly ITextRecognizer _recognizer;
        private readonly ImagePreprocessor _preprocessor;
        private readonly LabelParser _parser;
        private readonly ArticleGuesser _guesser;

        public ArticleOcrService(ArticleOcrOptions options)
            : this(options, null)
        {
        }

        /// <summary>Inject a recogniser (e.g. a fake for tests). When null, Tesseract is used.</summary>
        public ArticleOcrService(ArticleOcrOptions options, ITextRecognizer recognizer)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _recognizer = recognizer ?? new TesseractRecognizer(options.TessDataPath, options.Language, options.MaxEngines);
            _preprocessor = new ImagePreprocessor { MaxDimension = options.MaxImageDimension, MinDimension = options.MinImageDimension };
            var confusion = OcrConfusionModel.Default;
            _parser = new LabelParser(confusion);
            var master = ArticleMasterList.LoadCsv(options.MasterListPath);
            var grammar = new ArticleCodeGrammar(options.ArticlePatterns);
            _guesser = new ArticleGuesser(master, grammar, confusion)
            {
                Lambda = options.Lambda,
                VerifiedThreshold = options.VerifiedThreshold,
                VerifiedMargin = options.VerifiedMargin,
                RequireMasterListForVerification = options.RequireMasterListForVerification
            };
        }

        public ArticleGuesser Guesser { get { return _guesser; } }
        public LabelParser Parser { get { return _parser; } }

        /// <summary>Guess an article code from typed / pasted text without any image.</summary>
        public ArticleOcrResult GuessFromText(string text)
        {
            var sw = Stopwatch.StartNew();
            var result = new ArticleOcrResult { RawText = text, Fields = _parser.Parse(text) };
            var observations = new List<Observation>();
            var cleaned = LabelParser.CleanCode(text);
            if (result.Fields.Article != null || result.Fields.Quality != null)
            {
                if (result.Fields.Article != null) observations.Add(new Observation { Text = result.Fields.Article, Source = CandidateSource.ArticleField, Confidence = 0.6 });
                if (result.Fields.Quality != null) observations.Add(new Observation { Text = result.Fields.Quality, Source = CandidateSource.QualityField, Confidence = 0.6 });
            }
            else if (cleaned != null)
            {
                observations.Add(new Observation { Text = cleaned, Source = CandidateSource.UserText, Confidence = 0.6 });
            }
            Finish(result, observations);
            result.Elapsed = sw.Elapsed;
            return result;
        }

        /// <summary>Read a label from an encoded image (JPEG/PNG/BMP/TIFF stream, e.g. an uploaded photo).</summary>
        public ArticleOcrResult Process(Stream image)
        {
            List<ImageVariant> variants;
            using (var bmp = _preprocessor.Load(image))
            {
                variants = _preprocessor.BuildVariants(bmp);
            }
            return Process(variants);
        }

        /// <summary>Read a label from an already decoded grayscale raster.</summary>
        public ArticleOcrResult Process(GrayImage image)
        {
            return Process(_preprocessor.BuildVariants(image));
        }

        private ArticleOcrResult Process(List<ImageVariant> variants)
        {
            var sw = Stopwatch.StartNew();
            var result = new ArticleOcrResult();
            result.Diagnostics.Add("deskew angle " + variants[0].DeskewAngle.ToString("0.0") + " deg");

            foreach (var v in variants) RunPass(result, v);

            // Sideways / upside-down label: try the quadrant rotations of the most promising variant.
            var best = result.Passes.OrderByDescending(p => p.MeanConfidence).FirstOrDefault();
            if (best == null || best.MeanConfidence < _options.RotationFallbackConfidence)
            {
                var src = best == null ? variants[0] : variants.First(v => v.Name == best.Variant);
                foreach (var deg in new[] { 90, 270, 180 })
                {
                    RunPass(result, _preprocessor.RotateQuadrant(src, deg));
                }
                best = result.Passes.OrderByDescending(p => p.MeanConfidence).FirstOrDefault();
            }
            if (best == null)
            {
                // every pass threw (engine missing, corrupt image): report instead of crashing
                result.Fields = new LabelFields();
                result.Diagnostics.Add("no OCR pass succeeded");
                result.Elapsed = sw.Elapsed;
                return result;
            }

            result.BestVariant = best.Variant;
            result.RawText = best.Text;
            result.OcrConfidence = best.MeanConfidence;

            // Fields: take from the best pass, back-fill gaps from the others.
            var ordered = result.Passes.OrderByDescending(p => p.MeanConfidence).ToList();
            result.Fields = MergeFields(ordered.Select(p => _parser.Parse(p)).ToList());

            Finish(result, CollectObservations(ordered));
            result.Elapsed = sw.Elapsed;
            return result;
        }

        private void RunPass(ArticleOcrResult result, ImageVariant v)
        {
            try
            {
                var pass = _recognizer.Recognize(v.Image, v.Name);
                result.Passes.Add(pass);
                result.Diagnostics.Add(string.Format("pass {0}: conf {1:0.00}, {2} words, {3} ms", v.Name, pass.MeanConfidence, pass.Words.Count, (int)pass.Elapsed.TotalMilliseconds));
            }
            catch (Exception ex)
            {
                result.Diagnostics.Add("pass " + v.Name + " failed: " + ex.Message);
            }
        }

        private List<Observation> CollectObservations(IList<OcrPassResult> passes)
        {
            var observations = new List<Observation>();
            var grammar = _guesser.Grammar;

            foreach (var pass in passes)
            {
                double passWeight = 0.5 + 0.5 * pass.MeanConfidence;
                foreach (var f in _parser.ParseFields(pass))
                {
                    if (f.Key != "Article" && f.Key != "Quality") continue;
                    var code = LabelParser.CleanCode(f.Value);
                    if (code == null) continue;
                    // the value may carry trailing words ("RD258150 Mocha"): keep the first code-like token
                    var token = f.Value.Split(' ').Select(LabelParser.CleanCode).FirstOrDefault(t => t != null && grammar.IsPlausibleToken(t)) ?? code;
                    var symbols = token == code ? f.Symbols : null;
                    observations.Add(new Observation
                    {
                        Text = token,
                        Source = f.Key == "Article" ? CandidateSource.ArticleField : CandidateSource.QualityField,
                        Confidence = Math.Max(0.05, f.Confidence) * passWeight,
                        Symbols = symbols
                    });
                }

                // Any other token that is shaped like an article code (handles a missing / unreadable "Article" key).
                foreach (var w in pass.Words)
                {
                    var t = LabelParser.CleanCode(w.Text);
                    if (t == null || !grammar.IsPlausibleToken(t)) continue;
                    if (observations.Any(o => o.Text == t)) continue;
                    observations.Add(new Observation { Text = t, Source = CandidateSource.RawToken, Confidence = w.Confidence * passWeight, Symbols = w.Symbols });
                }
            }
            return observations;
        }

        private void Finish(ArticleOcrResult result, List<Observation> observations)
        {
            var ranked = _guesser.Guess(observations);
            result.Candidates.AddRange(ranked);
            if (ranked.Count > 0)
            {
                var top = ranked[0];
                result.ArticleCode = top.Code;
                result.Confidence = top.Probability;
                result.Method = top.Method;
                result.Source = top.Source;
                result.IsVerified = _guesser.IsVerified(ranked);
            }
            result.Diagnostics.Add(observations.Count + " observation(s): " + string.Join(", ", observations.Select(o => o.Text + "[" + o.Source + " " + o.Confidence.ToString("0.00") + "]")));
            if (_guesser.MasterList.Count > 0) result.Diagnostics.Add("master list: " + _guesser.MasterList.Count + " codes, shape " + _guesser.Grammar.DescribeProfile());
        }

        private static LabelFields MergeFields(IList<LabelFields> parsed)
        {
            if (parsed.Count == 0) return new LabelFields();
            var merged = parsed[0];
            foreach (var other in parsed.Skip(1))
            {
                merged.Quality = merged.Quality ?? other.Quality;
                merged.ColorCode = merged.ColorCode ?? other.ColorCode;
                merged.Customer = merged.Customer ?? other.Customer;
                merged.Batch = merged.Batch ?? other.Batch;
                merged.Roll = merged.Roll ?? other.Roll;
                merged.Barcode = merged.Barcode ?? other.Barcode;
                merged.Width = merged.Width ?? other.Width;
                merged.Quantity = merged.Quantity ?? other.Quantity;
                merged.Gross = merged.Gross ?? other.Gross;
                merged.Net = merged.Net ?? other.Net;
                merged.Weight = merged.Weight ?? other.Weight;
                merged.Free = merged.Free ?? other.Free;
                merged.PoNo = merged.PoNo ?? other.PoNo;
                merged.BpOrderNo = merged.BpOrderNo ?? other.BpOrderNo;
                merged.Article = merged.Article ?? other.Article;
                merged.ColorName = merged.ColorName ?? other.ColorName;
                merged.Remark = merged.Remark ?? other.Remark;
                merged.Date = merged.Date ?? other.Date;
                merged.WidthCm = merged.WidthCm ?? other.WidthCm;
                merged.QuantityMeters = merged.QuantityMeters ?? other.QuantityMeters;
                merged.GrossKg = merged.GrossKg ?? other.GrossKg;
                merged.NetKg = merged.NetKg ?? other.NetKg;
                merged.GramsPerSquareMeter = merged.GramsPerSquareMeter ?? other.GramsPerSquareMeter;
                merged.WeightConsistency = merged.WeightConsistency ?? other.WeightConsistency;
            }
            return merged;
        }

        public void Dispose()
        {
            _recognizer.Dispose();
        }
    }
}
