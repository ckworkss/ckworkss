using System;

namespace RemotePunch.Web.Models
{
    /// <summary>A circular geofence an employee is allowed to punch inside.</summary>
    public class Site
    {
        public int SiteId { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int RadiusMeters { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
