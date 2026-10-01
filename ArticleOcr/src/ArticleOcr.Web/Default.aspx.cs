using System;
using System.Web;
using System.Web.UI;
using ArticleOcr.Models;

namespace ArticleOcr.Web
{
    public partial class Default : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected void btnOcr_Click(object sender, EventArgs e)
        {
            pnlError.Visible = pnlResult.Visible = false;
            if (!fuImage.HasFile || fuImage.PostedFile.ContentLength == 0)
            {
                ShowError("Choose a photo of the label first.");
                return;
            }
            try
            {
                ArticleOcrResult result;
                using (var stream = fuImage.PostedFile.InputStream)
                {
                    result = OcrServiceHost.Service.Process(stream);
                }
                Show(result);
            }
            catch (Exception ex)
            {
                ShowError("OCR failed: " + ex.Message + Hint(ex));
            }
        }

        protected void btnGuess_Click(object sender, EventArgs e)
        {
            pnlError.Visible = pnlResult.Visible = false;
            var text = (txtGuess.Text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                ShowError("Type or paste some text first.");
                return;
            }
            try
            {
                Show(OcrServiceHost.Service.GuessFromText(text));
            }
            catch (Exception ex)
            {
                ShowError("Guess failed: " + ex.Message + Hint(ex));
            }
        }

        private void Show(ArticleOcrResult result)
        {
            litResult.Text = ResultRenderer.ToHtml(result, chkDebug.Checked);
            pnlResult.Visible = true;
        }

        private void ShowError(string message)
        {
            litError.Text = HttpUtility.HtmlEncode(message);
            pnlError.Visible = true;
        }

        private static string Hint(Exception ex)
        {
            var m = ex.ToString();
            if (m.Contains("tesseract50") || m.Contains("leptonica") || m.Contains("DllNotFoundException") || m.Contains("BadImageFormat"))
                return " (Install the Visual C++ 2019 x64/x86 Redistributable on the server and make sure bin\\x64 and bin\\x86 contain tesseract50.dll and leptonica-1.82.0.dll.)";
            if (m.Contains("traineddata"))
                return " (Put eng.traineddata in the tessdata folder configured in Web.config.)";
            return string.Empty;
        }
    }
}
