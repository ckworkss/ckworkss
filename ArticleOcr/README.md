# ArticleOcr – roll label OCR with article-code guessing (.NET Framework 4.8 / ASP.NET Web Forms)

Reads fabric roll labels (Best Pacific style: *Quality / Color / Customer / Batch / Roll / barcode / Width /
QTY / Gross / Net / PO No. / Article / Color / Remark*) from phone photos and returns the **article code**.
When the print is dirty, torn or blurred and the code cannot be read directly, the code is **inferred
statistically** from what *was* read, a master list of known articles, and the shape of valid codes.

```
ArticleOcr.sln
├─ src/ArticleOcr.Core    class library (net48): imaging, Tesseract, parsing, guessing
├─ src/ArticleOcr.Web     ASP.NET Web Forms site (net48): Default.aspx + Api/Ocr.ashx JSON endpoint
└─ tests/ArticleOcr.Tests MSTest suite (48 tests, no Tesseract needed)
```

## Getting started

1. Open `ArticleOcr.sln` in Visual Studio 2019/2022 with the **.NET Framework 4.8 developer pack** and the
   **ASP.NET and web development** workload. NuGet restores `Tesseract 5.2.0` (Charles Weld's wrapper).
2. Install the **Visual C++ 2019 Redistributable (x64 and x86)** on every machine that runs the site –
   the native `tesseract50.dll` / `leptonica-1.82.0.dll` need it. The build copies them to `bin\x64` and `bin\x86`.
3. `src/ArticleOcr.Web/tessdata/eng.traineddata` ships with the *tessdata_fast* English model (4 MB).
   For a few percent more accuracy replace it with the file from
   <https://github.com/tesseract-ocr/tessdata_best> (same name).
4. Put your real article codes in `src/ArticleOcr.Web/App_Data/articles.csv` (`Code,Description,Count`).
   The sample file contains the codes from the test photos. `Count` (how often the article is received) becomes
   the prior probability of that code. To load from SQL instead, build an `ArticleMasterList` in
   `OcrServiceHost` and pass it to `ArticleGuesser`.
5. Run (F5). Upload a photo, or paste a damaged code such as `R0258I5O` into the text box.

The site runs under IIS Express / IIS as a normal Web Forms application. Everything is configured in
`Web.config` `appSettings` (`Ocr:*` keys): tessdata folder, master list, article patterns, image size,
engine pool size, and the decision thresholds.

### JSON API (for a scanner app or the ERP)

```
POST /Api/Ocr.ashx            multipart/form-data, field "image"  (or a raw image/* body)
GET  /Api/Ocr.ashx?text=R0258I5O                                   (guess from text only)
add  &debug=1                 to include the raw OCR text and diagnostics
```

```json
{
  "articleCode": "RD258150",
  "confidence": 0.876,
  "verified": true,
  "method": "MasterList",
  "source": "ArticleField",
  "candidates": [
    { "code": "RD258150", "probability": 0.876, "observed": "RD25815O", "distance": 0.15, "method": "MasterList", "votes": 2, "matchesGrammar": true },
    { "code": "RD258160", "probability": 0.035, "observed": "RD25815O", "distance": 0.80, "method": "MasterList", "votes": 1, "matchesGrammar": true }
  ],
  "fields": { "Quality": "TE181065FX2-A2", "Batch": "V23016221", "Roll": "11", "Barcode": "423061001874", "Net": "17.00KG", "Article": "RD258150", "ColorName": "Mocha Taffy CSI 589" },
  "weightConsistency": 1.02,
  "elapsedMs": 2140
}
```

`verified` is true only when the top candidate has a high posterior probability, a clear margin over the
runner-up, and was read with at most minor (confusable) differences. Otherwise the result is a **guess** and the
`candidates` list shows the alternatives with their probabilities so an operator can confirm.

## How the pipeline works

1. **Preprocessing** (`Imaging/ImagePreprocessor.cs`): EXIF orientation, resize to a working size, percentile
   contrast stretch, **Sauvola adaptive threshold** (handles shadows and uneven light), **Otsu** global
   threshold, and **deskew** – the text angle is found by rotating a downsampled ink mask through −45…45° and
   choosing the angle whose horizontal projection profile is sharpest (largest Σ(Δrow)²), then refining to 0.25°.
   Three variants (gray, Sauvola, Otsu) go to the OCR engine; if all are weak the 90/180/270° rotations are tried.
2. **OCR** (`Recognition/TesseractRecognizer.cs`): Tesseract 5 LSTM through a small engine pool. Besides the
   text it keeps per-word and **per-character confidences and the engine's alternative characters**.
3. **Label parsing** (`Parsing/LabelParser.cs`): keys are matched fuzzily (`Artic1e`, `Qua1ity`, `Co1or`) with the
   OCR confusion model, a line may hold several fields (`Gross : 6.8KG Net : 6.1KG`), the barcode number and date
   are picked up by pattern, and numeric fields are parsed. A **weight consistency check** compares the stated net
   weight with GSM × width × length (ratio ≈ 1 means the numbers were read correctly).
4. **Guessing** (`Guessing/`): every observation (the *Article* line, the *Quality* line – on many labels they are
   the same – and any other token shaped like a code, from every OCR pass) is scored against hypotheses.

### The mathematics behind the guess

* **OCR confusion model** (`OcrConfusionModel.cs`) – a table of visual similarities for label fonts:
  `0↔O 0.85, 1↔I 0.85, 5↔S 0.85, 8↔B 0.85, 2↔Z 0.80, 6↔G 0.65, …`. Substitution cost is `1 − similarity`;
  punctuation and dirt specks are cheap to delete.
* **Confusion-weighted Damerau–Levenshtein distance** (`StringMetrics.cs`) – edit distance where every
  insertion, deletion, substitution and transposition is priced by the confusion model, so `RD25815O` is 0.15
  away from `RD258150` while `RD25815W` is 1.0 away. Jaro–Winkler and bigram cosine similarity are computed
  as well for reporting.
* **Code grammar** (`ArticleCodeGrammar.cs`, `PatternExemplars.cs`) – configurable regular expressions for valid
  codes (`^[A-Z]{2}\d{6}$`, `^[A-Z]{2}\d{6}[A-Z]{1,2}\d{1,3}(-[A-Z]\d{1,2})?$`, …). From the patterns
  (and the master list, when present) a **positional character-class profile** and a **class transition matrix**
  are learned with Laplace smoothing: P(letter | position) and P(class | previous class).
* **Grammar repair** (`GrammarRepair.cs`) – a Viterbi-style **beam search** over a hidden-Markov model: each read
  character is expanded into the characters it could really be (engine alternatives + confusion table), and every
  hypothesis is scored by `Σ log P(obs|char) + log 3·P(class|pos) + log 3·P(class|prev class)` with cheap drops for
  noise. The best strings that satisfy a pattern are returned – this is how `EA5118Z2FX3O` becomes
  `EA511822FX30` even with no master list.
* **Bayesian ranking** (`ArticleGuesser.cs`) – for every observation *o* and hypothesis *c*
  (master-list codes within a normalised distance, grammar repairs, and the raw text):

  ```
  w(c|o) = P(c) · exp(−λ·d(o,c)) · (0.5 + 0.5·conf(o)) · sourceWeight(o) · grammarFactor(c)
  ```

  `P(c)` is the smoothed frequency prior from the master list, `exp(−λ·d)` an exponential noise model over the
  weighted edit distance (λ = 2 ⇒ one full character error is e² ≈ 7.4× less likely), `conf(o)` the OCR confidence,
  `sourceWeight` trusts the *Article* line more than *Quality* or stray tokens. Identical codes from several
  observations / passes add their weights (independent votes). Weights are normalised together with an explicit
  "none of the above" hypothesis, so a single poor match cannot reach 100 %.
* **Decision** – *verified* when the top posterior ≥ 0.60, the margin to the runner-up ≥ 0.25, the read-to-code
  distance ≤ 0.9 (only confusable differences) and, optionally, the code exists in the master list.

All thresholds are properties on `ArticleGuesser` / `ArticleOcrOptions` and `Web.config`.

## Using the core library from other code

```csharp
var svc = new ArticleOcrService(new ArticleOcrOptions
{
    TessDataPath = @"C:\site\tessdata",
    MasterListPath = @"C:\site\App_Data\articles.csv"
});
using (var stream = File.OpenRead(@"label.jpg"))
{
    var r = svc.Process(stream);
    Console.WriteLine(r.ArticleCode + " " + r.Confidence + (r.IsVerified ? " verified" : " guess"));
    foreach (var c in r.Candidates) Console.WriteLine(c);
}
var guess = svc.GuessFromText("R0258I5O");   // no image needed
```

## Tests

`tests/ArticleOcr.Tests` covers the metrics, grammar learning, beam-search repair, Bayesian ranking, label
parsing, image filters / deskew, and the full pipeline with a fake OCR engine. Run them from Test Explorer or
`dotnet test` (requires the .NET Framework 4.8 targeting pack).

## Tuning tips

* Photograph the label as flat and as close as possible; the deskewer handles rotation but not strong perspective.
* If your codes have a different shape, change `Ocr:ArticlePatterns` – the grammar, repair and verification follow it.
* Add real codes to the master list: the more complete it is, the more often a damaged label still resolves.
* Raise `Ocr:Lambda` to be stricter, lower it to be more forgiving; set `Ocr:RequireMasterListForVerification`
  to `true` if only known articles may be auto-accepted.
* Each OCR pass takes 0.5–2 s per image on a typical server; `Ocr:MaxEngines` caps concurrent engines.
