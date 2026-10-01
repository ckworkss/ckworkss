using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;
using ArticleOcr.Models;

namespace ArticleOcr.Web.Api
{
    /// <summary>
    /// JSON endpoint for scanners / mobile apps.
    /// <list type="bullet">
    ///   <item><c>POST Api/Ocr.ashx</c> multipart form with field <c>image</c> (or a raw image body) -> OCR result.</item>
    ///   <item><c>GET  Api/Ocr.ashx?text=R0258I5O</c> -> guess from text only.</item>
    ///   <item>Add <c>debug=1</c> to include raw OCR text and diagnostics.</item>
    /// </list>
    /// </summary>
    public class OcrHandler : IHttpHandler
    {
        public bool IsReusable { get { return true; } }

        public void ProcessRequest(HttpContext context)
        {
            var response = context.Response;
            response.ContentType = "application/json";
            response.Charset = "utf-8";
            response.Cache.SetCacheability(HttpCacheability.NoCache);
            bool debug = context.Request["debug"] == "1" || string.Equals(context.Request["debug"], "true", StringComparison.OrdinalIgnoreCase);

            try
            {
                ArticleOcrResult result;
                var text = context.Request["text"];
                var file = context.Request.Files.Count > 0 ? (context.Request.Files["image"] ?? context.Request.Files[0]) : null;

                if (file != null && file.ContentLength > 0)
                {
                    using (var s = file.InputStream) result = OcrServiceHost.Service.Process(s);
                }
                else if (context.Request.HttpMethod == "POST" && context.Request.ContentLength > 0 && (context.Request.ContentType ?? "").StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    using (var ms = new MemoryStream())
                    {
                        context.Request.InputStream.CopyTo(ms);
                        ms.Position = 0;
                        result = OcrServiceHost.Service.Process(ms);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(text))
                {
                    result = OcrServiceHost.Service.GuessFromText(text);
                }
                else
                {
                    response.StatusCode = 400;
                    Write(response, new Dictionary<string, object> { { "error", "Send an image file in field 'image' (POST) or a 'text' parameter." } });
                    return;
                }

                Write(response, ResultRenderer.ToJsonObject(result, debug));
            }
            catch (Exception ex)
            {
                response.StatusCode = 500;
                Write(response, new Dictionary<string, object> { { "error", ex.Message }, { "type", ex.GetType().Name } });
            }
        }

        private static void Write(HttpResponse response, object payload)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            response.Write(serializer.Serialize(payload));
        }
    }
}
