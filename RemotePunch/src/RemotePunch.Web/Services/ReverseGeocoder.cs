using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;

namespace RemotePunch.Web.Services
{
    /// <summary>
    /// Turns a coordinate into a human-readable address. Entirely optional: the
    /// geofence decision never depends on it, so any failure or timeout just
    /// leaves the address blank. Results are cached in dbo.GeocodeCache.
    /// </summary>
    public class ReverseGeocoder
    {
        private readonly GeocodeCacheRepository _cache = new GeocodeCacheRepository();

        public string Resolve(double latitude, double longitude)
        {
            string provider = AppConfig.GeocodeProvider;
            if (string.IsNullOrEmpty(provider) || provider.Equals("None", StringComparison.OrdinalIgnoreCase))
                return null;

            string key = GeoMath.GeocodeCacheKey(latitude, longitude);
            string cached = _cache.Get(key);
            if (!string.IsNullOrEmpty(cached)) return cached;

            string address = null;
            try
            {
                if (provider.Equals("Nominatim", StringComparison.OrdinalIgnoreCase))
                    address = QueryNominatim(latitude, longitude);
                else if (provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
                    address = QueryGoogle(latitude, longitude);
            }
            catch (WebException ex)
            {
                Trace.TraceWarning("Reverse geocode failed: " + ex.Message);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Reverse geocode error: " + ex.Message);
            }

            if (!string.IsNullOrEmpty(address))
            {
                address = ClientInfo.Truncate(address, 400);
                _cache.Put(key, address);
            }
            return address;
        }

        private string QueryNominatim(double latitude, double longitude)
        {
            string url = "https://nominatim.openstreetmap.org/reverse?format=jsonv2&zoom=18&addressdetails=0" +
                         "&lat=" + Num(latitude) + "&lon=" + Num(longitude);

            // Nominatim's usage policy requires an identifying User-Agent.
            string contact = AppConfig.GeocodeContactEmail;
            string userAgent = "RemotePunch/1.0" + (string.IsNullOrEmpty(contact) ? "" : " (" + contact + ")");

            string json = HttpGet(url, userAgent);
            if (string.IsNullOrEmpty(json)) return null;

            Dictionary<string, object> parsed = Json.Deserialize(json);
            return Json.Str(parsed, "display_name", 400);
        }

        private string QueryGoogle(double latitude, double longitude)
        {
            string apiKey = AppConfig.GoogleApiKey;
            if (string.IsNullOrEmpty(apiKey)) return null;

            string url = "https://maps.googleapis.com/maps/api/geocode/json?latlng=" +
                         Num(latitude) + "," + Num(longitude) +
                         "&key=" + Uri.EscapeDataString(apiKey);

            string json = HttpGet(url, "RemotePunch/1.0");
            if (string.IsNullOrEmpty(json)) return null;

            Dictionary<string, object> parsed = Json.Deserialize(json);
            object resultsObject;
            if (!parsed.TryGetValue("results", out resultsObject)) return null;

            object[] results = resultsObject as object[];
            if (results == null || results.Length == 0) return null;

            Dictionary<string, object> first = results[0] as Dictionary<string, object>;
            return first == null ? null : Json.Str(first, "formatted_address", 400);
        }

        private string HttpGet(string url, string userAgent)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = userAgent;
            request.Accept = "application/json";
            request.Timeout = AppConfig.GeocodeTimeoutMs;
            request.ReadWriteTimeout = AppConfig.GeocodeTimeoutMs;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK) return null;
                using (Stream stream = response.GetResponseStream())
                {
                    if (stream == null) return null;
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        private static string Num(double value)
        {
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }
    }
}
