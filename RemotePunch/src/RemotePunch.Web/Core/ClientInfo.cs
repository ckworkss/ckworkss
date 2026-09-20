using System;
using System.Text;
using System.Web;

namespace RemotePunch.Web.Core
{
    /// <summary>Per-request facts about the caller: address, agent, device hash.</summary>
    public class ClientInfo
    {
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public bool ForwardedForPresent { get; set; }

        public static ClientInfo FromRequest(HttpRequest request)
        {
            ClientInfo info = new ClientInfo();
            if (request == null) return info;

            info.IpAddress = ResolveIp(request);
            info.UserAgent = Truncate(request.UserAgent, 500);
            info.ForwardedForPresent = !string.IsNullOrEmpty(request.Headers["X-Forwarded-For"]);
            return info;
        }

        /// <summary>
        /// Client address, honouring X-Forwarded-For only for its left-most entry
        /// (the original client when the app sits behind a trusted reverse proxy).
        /// </summary>
        private static string ResolveIp(HttpRequest request)
        {
            string forwarded = request.Headers["X-Forwarded-For"];
            if (!string.IsNullOrEmpty(forwarded))
            {
                string first = forwarded.Split(',')[0].Trim();
                if (first.Length > 0 && first.Length <= 64) return first;
            }
            return Truncate(request.UserHostAddress, 64);
        }

        /// <summary>
        /// Stable-ish device hash built from signals the browser reports. It is a
        /// correlation aid, not an authentication factor: a determined user can
        /// change any of the inputs, which is exactly why a new hash only raises
        /// a flag rather than blocking the punch.
        /// </summary>
        public static string Fingerprint(string clientSignals, string userAgent)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(clientSignals ?? string.Empty).Append('|').Append(userAgent ?? string.Empty);
            return PasswordHasher.Sha256Hex(sb.ToString());
        }

        public static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= max ? value : value.Substring(0, max);
        }
    }
}
