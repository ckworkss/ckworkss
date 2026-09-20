using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web;
using RemotePunch.Web.Core;
using RemotePunch.Web.Models;
using RemotePunch.Web.Services;

namespace RemotePunch.Web.Api
{
    /// <summary>
    /// The endpoint punch.js posts to. It authenticates the caller, parses the
    /// raw location payload and hands it to PunchService, which owns every
    /// decision about whether the punch counts.
    /// </summary>
    public class PunchHandler : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                Json.Write(response, new PunchResult { ok = false, status = "Error", message = "POST expected." }, 405);
                return;
            }

            AppUser user = AppUser.Current;
            if (user == null)
            {
                Json.Write(response, new PunchResult
                {
                    ok = false,
                    status = "Error",
                    message = "Your session has expired. Sign in again to punch."
                }, 401);
                return;
            }

            // The auth cookie is SameSite=Lax and this custom header cannot be
            // set by a cross-site form post, so a forged punch from another
            // origin cannot reach this code.
            if (string.IsNullOrEmpty(request.Headers["X-RemotePunch"]))
            {
                Json.Write(response, new PunchResult
                {
                    ok = false,
                    status = "Error",
                    message = "This request did not come from the punch page."
                }, 400);
                return;
            }

            if (user.MustChangePassword)
            {
                Json.Write(response, new PunchResult
                {
                    ok = false,
                    status = "Error",
                    message = "Set a new password before punching."
                }, 403);
                return;
            }

            try
            {
                PunchRequest punchRequest = ReadRequest(request);
                PunchService service = new PunchService();
                PunchResult result = service.Register(user.EmployeeId, punchRequest, ClientInfo.FromRequest(request));
                Json.Write(response, result, 200);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Punch failed: " + ex);
                Json.Write(response, new PunchResult
                {
                    ok = false,
                    status = "Error",
                    message = "The punch could not be recorded. Try again, and tell IT if it keeps failing."
                }, 500);
            }
        }

        private static PunchRequest ReadRequest(HttpRequest request)
        {
            string body;
            using (StreamReader reader = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }

            Dictionary<string, object> data = Json.Deserialize(body);

            PunchRequest result = new PunchRequest
            {
                RequestedType = Json.Str(data, "requestedType", 10) ?? "AUTO",
                ClientRequestId = Json.Str(data, "clientRequestId", 64),
                DeviceSignals = Json.Str(data, "deviceSignals", 500),
                DeviceLabel = Json.Str(data, "deviceLabel", 200),
                Note = Json.Str(data, "note", 500),
                SelfieBase64 = Json.Str(data, "selfieBase64", int.MaxValue)
            };

            object warningsObject;
            if (data.TryGetValue("clientWarnings", out warningsObject))
            {
                object[] warnings = warningsObject as object[];
                if (warnings != null)
                {
                    foreach (object warning in warnings)
                    {
                        if (warning != null) result.ClientWarnings.Add(Convert.ToString(warning));
                    }
                }
            }

            object fixObject;
            if (data.TryGetValue("fix", out fixObject))
            {
                Dictionary<string, object> fixData = fixObject as Dictionary<string, object>;
                if (fixData != null) result.Fix = ReadFix(fixData);
            }
            return result;
        }

        private static GeoFix ReadFix(Dictionary<string, object> data)
        {
            GeoFix fix = new GeoFix
            {
                Latitude = Json.Num(data, "latitude").GetValueOrDefault(),
                Longitude = Json.Num(data, "longitude").GetValueOrDefault(),
                AccuracyMeters = Json.Num(data, "accuracy"),
                Altitude = Json.Num(data, "altitude"),
                AltitudeAccuracy = Json.Num(data, "altitudeAccuracy"),
                Speed = Json.Num(data, "speed"),
                Heading = Json.Num(data, "heading"),
                AgeSeconds = Json.Num(data, "ageSeconds")
            };

            double? sampleCount = Json.Num(data, "sampleCount");
            if (sampleCount.HasValue) fix.SampleCount = (int)sampleCount.Value;

            // position.timestamp is milliseconds since the Unix epoch, taken
            // from the device clock.
            double? timestamp = Json.Num(data, "timestamp");
            if (timestamp.HasValue && timestamp.Value > 0)
            {
                try
                {
                    fix.PositionTimestampUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                        .AddMilliseconds(timestamp.Value);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // A device clock far outside any sane range: ignore the value,
                    // the server's own clock decides when the punch happened.
                    fix.PositionTimestampUtc = null;
                }
            }
            return fix;
        }
    }
}
