using System;
using ArticleOcr.Imaging;
using ArticleOcr.Models;

namespace ArticleOcr.Recognition
{
    /// <summary>Abstraction over the OCR engine so the pipeline can be tested without Tesseract.</summary>
    public interface ITextRecognizer : IDisposable
    {
        /// <summary>Recognise text in a preprocessed grayscale image.</summary>
        OcrPassResult Recognize(GrayImage image, string variantName);
    }
}
