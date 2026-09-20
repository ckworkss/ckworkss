using System.Collections.Generic;

namespace RemotePunch.Web.Services
{
    /// <summary>
    /// Every integrity finding a punch can carry, with the risk it adds and the
    /// wording shown to reviewers. Keeping them in one place means the punch
    /// screen, the reports and the audit trail all say the same thing.
    /// </summary>
    public static class FlagCodes
    {
        public const string OutsideGeofence = "OUTSIDE_GEOFENCE";
        public const string NoSiteConfigured = "NO_SITE_CONFIGURED";
        public const string LowAccuracy = "LOW_ACCURACY";
        public const string NoAccuracy = "NO_ACCURACY_REPORTED";
        public const string StaleFix = "STALE_FIX";
        public const string ImpossibleTravel = "IMPOSSIBLE_TRAVEL";
        public const string IdenticalCoordinates = "IDENTICAL_COORDINATES";
        public const string UntrustedDevice = "UNTRUSTED_DEVICE";
        public const string GeolocationApiPatched = "GEOLOCATION_API_PATCHED";
        public const string SuspiciousFixShape = "SUSPICIOUS_FIX_SHAPE";
        public const string ClockSkew = "DEVICE_CLOCK_SKEW";
        public const string OutsideShift = "OUTSIDE_SHIFT_HOURS";
        public const string RemotePunch = "REMOTE_PUNCH_ALLOWED";
        public const string ProxyHeader = "FORWARDED_REQUEST";
        public const string NoSelfie = "SELFIE_MISSING";

        private static readonly Dictionary<string, int> Risk = new Dictionary<string, int>
        {
            { OutsideGeofence,      50 },
            { NoSiteConfigured,     20 },
            { LowAccuracy,          15 },
            { NoAccuracy,           15 },
            { StaleFix,             25 },
            { ImpossibleTravel,     60 },
            { IdenticalCoordinates, 25 },
            { UntrustedDevice,      20 },
            { GeolocationApiPatched,70 },
            { SuspiciousFixShape,   15 },
            { ClockSkew,            20 },
            { OutsideShift,         10 },
            { RemotePunch,           0 },
            { ProxyHeader,           5 },
            { NoSelfie,             10 }
        };

        private static readonly Dictionary<string, string> Text = new Dictionary<string, string>
        {
            { OutsideGeofence,       "Outside every geofence assigned to this employee." },
            { NoSiteConfigured,      "No active site is assigned, so the location could not be matched." },
            { LowAccuracy,           "The position fix was low quality." },
            { NoAccuracy,            "The browser reported no accuracy for the fix." },
            { StaleFix,              "The position fix was older than the allowed age." },
            { ImpossibleTravel,      "Distance from the previous punch implies an impossible speed." },
            { IdenticalCoordinates,  "Coordinates are byte-identical to the previous punch." },
            { UntrustedDevice,       "Punched from a device an administrator has not trusted." },
            { GeolocationApiPatched, "The browser's geolocation API was not the native implementation." },
            { SuspiciousFixShape,    "The fix has the shape of a synthetic location (no altitude, speed or heading)." },
            { ClockSkew,             "The device clock differs materially from server time." },
            { OutsideShift,          "Punched outside the employee's shift window." },
            { RemotePunch,           "Remote punching is enabled for this employee." },
            { ProxyHeader,           "Request arrived through a proxy or forwarder." },
            { NoSelfie,              "No photo was captured with this punch." }
        };

        public static int RiskOf(string code)
        {
            int risk;
            return Risk.TryGetValue(code ?? string.Empty, out risk) ? risk : 10;
        }

        public static string Describe(string code)
        {
            string text;
            return Text.TryGetValue(code ?? string.Empty, out text) ? text : code;
        }

        /// <summary>Turns the stored comma-separated codes into readable sentences.</summary>
        public static List<string> DescribeAll(string storedCodes)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(storedCodes)) return result;

            foreach (string code in storedCodes.Split(','))
            {
                string trimmed = code.Trim();
                if (trimmed.Length > 0) result.Add(Describe(trimmed));
            }
            return result;
        }
    }
}
