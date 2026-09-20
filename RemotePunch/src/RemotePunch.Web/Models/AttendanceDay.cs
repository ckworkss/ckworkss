using System;

namespace RemotePunch.Web.Models
{
    /// <summary>One row of dbo.vw_DailyAttendance.</summary>
    public class AttendanceDay
    {
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; }
        public string FullName { get; set; }
        public DateTime WorkDateLocal { get; set; }
        public DateTime? FirstInLocal { get; set; }
        public DateTime? LastOutLocal { get; set; }
        public int WorkedMinutes { get; set; }
        public int PunchCount { get; set; }
        public int FlaggedCount { get; set; }
        public int? LateMinutes { get; set; }

        public string WorkedHoursText
        {
            get { return (WorkedMinutes / 60).ToString("00") + "h " + (WorkedMinutes % 60).ToString("00") + "m"; }
        }
    }
}
