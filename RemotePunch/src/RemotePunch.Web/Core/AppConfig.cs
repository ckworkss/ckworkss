using System;
using System.Configuration;
using System.Globalization;

namespace RemotePunch.Web.Core
{
    /// <summary>
    /// Typed, defaulted access to everything in &lt;appSettings&gt;. A missing or
    /// malformed key falls back to the documented default rather than throwing,
    /// so a bad edit to Web.config cannot take the punch page down.
    /// </summary>
    public static class AppConfig
    {
        // ---- time -------------------------------------------------------
        public static string TimeZoneId { get { return GetString("App.TimeZoneId", "India Standard Time"); } }
        public static int WorkDayRolloverHour { get { return GetInt("App.WorkDayRolloverHour", 4, 0, 12); } }

        // ---- geofence ---------------------------------------------------
        public static double MaxAccuracyMeters { get { return GetDouble("Geo.MaxAccuracyMeters", 150); } }
        public static double WarnAccuracyMeters { get { return GetDouble("Geo.WarnAccuracyMeters", 60); } }
        public static bool AddAccuracyToRadius { get { return GetBool("Geo.AddAccuracyToRadius", true); } }
        public static double MaxAccuracyBonusMeters { get { return GetDouble("Geo.MaxAccuracyBonusMeters", 75); } }
        public static int MaxPositionAgeSeconds { get { return GetInt("Geo.MaxPositionAgeSeconds", 120, 5, 3600); } }
        public static bool RejectOutsideGeofence { get { return GetBool("Geo.RejectOutsideGeofence", true); } }

        // ---- anti-spoofing ---------------------------------------------
        public static double MaxImpliedSpeedKmh { get { return GetDouble("Fraud.MaxImpliedSpeedKmh", 900); } }
        public static bool FlagIdenticalCoordinates { get { return GetBool("Fraud.FlagIdenticalCoordinates", true); } }
        public static bool FlagUntrustedDevice { get { return GetBool("Fraud.FlagUntrustedDevice", true); } }
        public static bool AutoTrustFirstDevice { get { return GetBool("Fraud.AutoTrustFirstDevice", true); } }
        public static int FlagRiskThreshold { get { return GetInt("Fraud.FlagRiskThreshold", 40, 1, 1000); } }
        public static int RejectRiskThreshold { get { return GetInt("Fraud.RejectRiskThreshold", 100, 1, 1000); } }
        public static int MinPunchIntervalSeconds { get { return GetInt("Punch.MinIntervalSeconds", 60, 0, 86400); } }

        // ---- selfie -----------------------------------------------------
        public static bool SelfieEnabled { get { return GetBool("Selfie.Enabled", true); } }
        public static bool SelfieRequiredForAll { get { return GetBool("Selfie.RequiredForAll", false); } }
        public static int SelfieMaxBytes { get { return GetInt("Selfie.MaxBytes", 1500000, 10000, 8000000); } }
        public static string SelfieStoragePath { get { return GetString("Selfie.StoragePath", "~/Uploads/Selfies"); } }

        // ---- reverse geocoding -----------------------------------------
        public static string GeocodeProvider { get { return GetString("Geocode.Provider", "None"); } }
        public static string GoogleApiKey { get { return GetString("Geocode.GoogleApiKey", ""); } }
        public static int GeocodeTimeoutMs { get { return GetInt("Geocode.TimeoutMs", 3000, 500, 20000); } }
        public static string GeocodeContactEmail { get { return GetString("Geocode.ContactEmail", ""); } }

        // ---- login ------------------------------------------------------
        public static int MaxFailedAttempts { get { return GetInt("Auth.MaxFailedAttempts", 5, 1, 100); } }
        public static int LockoutMinutes { get { return GetInt("Auth.LockoutMinutes", 15, 1, 1440); } }

        /// <summary>Time zone used for every local time the app displays or stores.</summary>
        public static TimeZoneInfo TimeZone
        {
            get
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
                catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
                catch (InvalidTimeZoneException) { return TimeZoneInfo.Local; }
            }
        }

        public static DateTime ToLocal(DateTime utc)
        {
            DateTime unspecified = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(unspecified, TimeZone);
        }

        // ---- readers ----------------------------------------------------
        public static string GetString(string key, string fallback)
        {
            string raw = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();
        }

        public static int GetInt(string key, int fallback, int min, int max)
        {
            int value;
            string raw = ConfigurationManager.AppSettings[key];
            if (raw == null || !int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return fallback;
            if (value < min || value > max) return fallback;
            return value;
        }

        public static double GetDouble(string key, double fallback)
        {
            double value;
            string raw = ConfigurationManager.AppSettings[key];
            if (raw == null || !double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return fallback;
            return value;
        }

        public static bool GetBool(string key, bool fallback)
        {
            bool value;
            string raw = ConfigurationManager.AppSettings[key];
            if (raw == null || !bool.TryParse(raw.Trim(), out value)) return fallback;
            return value;
        }
    }
}
