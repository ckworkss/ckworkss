using System;

namespace RemotePunch.Web.Models
{
    /// <summary>
    /// A geolocation reading exactly as the browser's Geolocation API reported
    /// it. Nothing here is trusted: every field is re-checked server side.
    /// </summary>
    public class GeoFix
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? AccuracyMeters { get; set; }
        public double? Altitude { get; set; }
        public double? AltitudeAccuracy { get; set; }
        public double? Speed { get; set; }
        public double? Heading { get; set; }
        /// <summary>position.timestamp, converted to UTC. Device clock - treat with suspicion.</summary>
        public DateTime? PositionTimestampUtc { get; set; }
        /// <summary>Seconds between the fix and the request, measured on the client.</summary>
        public double? AgeSeconds { get; set; }
        /// <summary>How many candidate readings the client took before settling on this one.</summary>
        public int SampleCount { get; set; }
    }
}
