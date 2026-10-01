using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ArticleOcr.Imaging;
using ArticleOcr.Models;
using Tesseract;

namespace ArticleOcr.Recognition
{
    /// <summary>
    /// Tesseract 5 (LSTM) recogniser with a small engine pool. <see cref="TesseractEngine"/> is not thread safe,
    /// so each request borrows an engine from the pool; the pool size caps concurrent OCR work on a web server.
    /// Per-symbol alternative choices are captured so the guesser can use the engine's own uncertainty.
    /// </summary>
    public sealed class TesseractRecognizer : ITextRecognizer
    {
        private readonly string _tessData;
        private readonly string _language;
        private readonly ConcurrentBag<TesseractEngine> _pool = new ConcurrentBag<TesseractEngine>();
        private readonly SemaphoreSlim _gate;
        private bool _disposed;

        public PageSegMode SegmentationMode { get; set; } = PageSegMode.SingleBlock;

        public TesseractRecognizer(string tessDataPath, string language = "eng", int maxEngines = 0)
        {
            if (string.IsNullOrEmpty(tessDataPath)) throw new ArgumentNullException(nameof(tessDataPath));
            if (!Directory.Exists(tessDataPath)) throw new DirectoryNotFoundException("tessdata directory not found: " + tessDataPath);
            var traineddata = Path.Combine(tessDataPath, language + ".traineddata");
            if (!File.Exists(traineddata)) throw new FileNotFoundException("Missing language file " + traineddata + ". Download it from https://github.com/tesseract-ocr/tessdata_fast", traineddata);
            _tessData = tessDataPath;
            _language = language;
            if (maxEngines <= 0) maxEngines = Math.Max(1, Math.Min(4, Environment.ProcessorCount));
            _gate = new SemaphoreSlim(maxEngines, maxEngines);
        }

        private TesseractEngine Rent()
        {
            TesseractEngine e;
            if (_pool.TryTake(out e)) return e;
            e = new TesseractEngine(_tessData, _language, EngineMode.LstmOnly);
            e.SetVariable("user_defined_dpi", "300");
            e.SetVariable("preserve_interword_spaces", "1");
            // label fonts are upper case + digits; this nudges the LSTM without hard-blocking anything
            e.SetVariable("tessedit_char_blacklist", "{}[]<>");
            return e;
        }

        private void Return(TesseractEngine e)
        {
            if (_disposed) e.Dispose(); else _pool.Add(e);
        }

        public OcrPassResult Recognize(GrayImage image, string variantName)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            var sw = Stopwatch.StartNew();
            var encodedImage = image.ToPng();
            var result = new OcrPassResult { Variant = variantName };

            _gate.Wait();
            TesseractEngine engine = null;
            try
            {
                engine = Rent();
                using (var pix = Pix.LoadFromMemory(encodedImage))
                using (var page = engine.Process(pix, SegmentationMode))
                {
                    result.Text = page.GetText() ?? string.Empty;
                    result.MeanConfidence = page.GetMeanConfidence();
                    CollectWords(page, result.Words);
                }
            }
            finally
            {
                if (engine != null) Return(engine);
                _gate.Release();
            }
            result.Elapsed = sw.Elapsed;
            return result;
        }

        private static void CollectWords(Page page, List<OcrWord> words)
        {
            using (var iter = page.GetIterator())
            {
                iter.Begin();
                int line = 0;
                do
                {
                    do
                    {
                        do
                        {
                            OcrWord word = null;
                            do
                            {
                                if (iter.IsAtBeginningOf(PageIteratorLevel.Word))
                                {
                                    word = new OcrWord
                                    {
                                        Text = (iter.GetText(PageIteratorLevel.Word) ?? string.Empty).Trim(),
                                        Confidence = iter.GetConfidence(PageIteratorLevel.Word) / 100.0,
                                        Line = line
                                    };
                                    if (word.Text.Length > 0) words.Add(word);
                                }
                                if (word == null) continue;
                                var symText = iter.GetText(PageIteratorLevel.Symbol);
                                if (string.IsNullOrEmpty(symText)) continue;
                                var sym = new OcrSymbol { Char = symText[0], Confidence = iter.GetConfidence(PageIteratorLevel.Symbol) / 100.0 };
                                try
                                {
                                    using (var choices = iter.GetChoiceIterator())
                                    {
                                        if (choices != null)
                                        {
                                            do
                                            {
                                                var t = choices.GetText();
                                                if (!string.IsNullOrEmpty(t))
                                                    sym.Choices.Add(new KeyValuePair<char, double>(t[0], choices.GetConfidence() / 100.0));
                                            } while (choices.Next());
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    // choice iterator is optional; the confusion model covers the gap
                                }
                                word.Symbols.Add(sym);
                            } while (iter.Next(PageIteratorLevel.Word, PageIteratorLevel.Symbol));
                        } while (iter.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word));
                        line++;
                    } while (iter.Next(PageIteratorLevel.Para, PageIteratorLevel.TextLine));
                } while (iter.Next(PageIteratorLevel.Block, PageIteratorLevel.Para));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            TesseractEngine e;
            while (_pool.TryTake(out e)) e.Dispose();
            _gate.Dispose();
        }
    }
}
