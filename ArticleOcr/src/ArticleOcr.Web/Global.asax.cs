using System;
using System.Web;

namespace ArticleOcr.Web
{
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            // Warm the engine pool so the first request is not slow. Errors (missing tessdata, missing VC++
            // runtime) surface on the first page load instead of here so the site can still start.
            try { OcrServiceHost.Warmup(); } catch (Exception) { }
        }

        protected void Application_End(object sender, EventArgs e)
        {
            OcrServiceHost.Shutdown();
        }
    }
}
