using System;

namespace RemotePunch.Web.Models
{
    public class Punch
    {
        public long PunchId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; }
        public string EmployeeName { get; set; }
        public string PunchType { get; set; }       // IN | OUT
        public string Status { get; set; }          // Accepted | Flagged | Rejected
        public DateTime PunchTimeUtc { get; set; }
        public DateTime PunchTimeLocal { get; set; }
        public DateTime WorkDateLocal { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? AccuracyMeters { get; set; }
        public double? Altitude { get; set; }
        public double? AltitudeAccuracy { get; set; }
        public double? Speed { get; set; }
        public double? Heading { get; set; }
        public DateTime? PositionTimestamp { get; set; }

        public int? SiteId { get; set; }
        public string SiteName { get; set; }
        public double? DistanceMeters { get; set; }
        public bool IsWithinGeofence { get; set; }
        public string ResolvedAddress { get; set; }

        public int RiskScore { get; set; }
        public string FlagReasons { get; set; }
        public string RejectReason { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string DeviceFingerprint { get; set; }
        public string SelfiePath { get; set; }
        public string ClientRequestId { get; set; }
        public string Note { get; set; }

        public string ReviewStatus { get; set; }
        public int? ReviewedByEmpId { get; set; }
        public string ReviewedByName { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public string ReviewNote { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public bool HasSelfie { get { return !string.IsNullOrEmpty(SelfiePath); } }
    }
}
