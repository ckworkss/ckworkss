using System;

namespace RemotePunch.Web.Models
{
    public class Device
    {
        public int DeviceId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string Fingerprint { get; set; }
        public string Label { get; set; }
        public bool IsTrusted { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime FirstSeenUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public int PunchCount { get; set; }
    }
}
