using System.Collections.Generic;

namespace RemotePunch.Web.Models
{
    /// <summary>The JSON the punch endpoint returns. Field names match punch.js.</summary>
    public class PunchResult
    {
        public bool ok { get; set; }
        public string status { get; set; }          // Accepted | Flagged | Rejected | Error
        public string punchType { get; set; }
        public string message { get; set; }
        public string timeLocal { get; set; }
        public string workDate { get; set; }
        public string siteName { get; set; }
        public double? distanceMeters { get; set; }
        public bool withinGeofence { get; set; }
        public string address { get; set; }
        public int riskScore { get; set; }
        public List<string> flags { get; set; }
        public string nextAction { get; set; }      // the punch type the user may do next
        public long punchId { get; set; }
        public bool duplicate { get; set; }         // true when an idempotent replay was returned

        public PunchResult()
        {
            flags = new List<string>();
        }
    }
}
