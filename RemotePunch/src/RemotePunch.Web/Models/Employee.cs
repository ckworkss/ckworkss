using System;

namespace RemotePunch.Web.Models
{
    public class Employee
    {
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public int PasswordIterations { get; set; }
        public bool MustChangePassword { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public TimeSpan? ShiftStartLocal { get; set; }
        public TimeSpan? ShiftEndLocal { get; set; }
        /// <summary>When false, a punch outside every assigned geofence is refused.</summary>
        public bool AllowRemotePunch { get; set; }
        public bool RequireSelfie { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutUntilUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public bool IsLockedOut
        {
            get { return LockoutUntilUtc.HasValue && LockoutUntilUtc.Value > DateTime.UtcNow; }
        }
    }
}
