using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using System.Web.Script.Serialization;

namespace RemotePunch.Web.Core
{
    /// <summary>
    /// Small JSON helpers over the framework's JavaScriptSerializer, so the app
    /// needs no third-party package to talk to its own AJAX endpoints.
    /// </summary>
    public static class Json
    {
        public static string Serialize(object value)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            return serializer.Serialize(value);
        }

        public static Dictionary<string, object> Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object>();
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> result = serializer.Deserialize<Dictionary<string, object>>(json);
            return result ?? new Dictionary<string, object>();
        }


        // ---- readers for a deserialized payload -------------------------
        // JavaScriptSerializer hands back int/decimal/double/string depending on
        // how the number was written, so every read goes through Convert.

        public static string Str(IDictionary<string, object> data, string key, int maxLength)
        {
            object value;
            if (data == null || !data.TryGetValue(key, out value) || value == null) return null;
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (text == null) return null;
            text = text.Trim();
            if (text.Length == 0) return null;
            return text.Length <= maxLength ? text : text.Substring(0, maxLength);
        }

        public static double? Num(IDictionary<string, object> data, string key)
        {
            object value;
            if (data == null || !data.TryGetValue(key, out value) || value == null) return null;

            if (value is double) return (double)value;
            if (value is int) return (int)value;
            if (value is long) return (long)value;
            if (value is decimal) return (double)(decimal)value;

            double parsed;
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(text) &&
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
                !double.IsNaN(parsed) && !double.IsInfinity(parsed))
            {
                return parsed;
            }
            return null;
        }

        public static bool Flag(IDictionary<string, object> data, string key, bool fallback)
        {
            object value;
            if (data == null || !data.TryGetValue(key, out value) || value == null) return fallback;
            if (value is bool) return (bool)value;

            bool parsed;
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return bool.TryParse(text, out parsed) ? parsed : fallback;
        }

        public static void Write(HttpResponse response, object payload, int statusCode)
        {
            response.Clear();
            response.StatusCode = statusCode;
            response.TrySkipIisCustomErrors = true;
            response.ContentType = "application/json";
            response.Cache.SetCacheability(HttpCacheability.NoCache);
            response.Write(Serialize(payload));
        }
    }
}
