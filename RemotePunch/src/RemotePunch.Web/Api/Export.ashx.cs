using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;
using RemotePunch.Web.Models;
using RemotePunch.Web.Services;

namespace RemotePunch.Web.Api
{
    /// <summary>
    /// CSV export for the reports screens: kind=punches or kind=attendance.
    /// An employee may only export their own rows; managers and admins may
    /// export anyone's.
    /// </summary>
    public class ExportHandler : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            AppUser user = AppUser.Current;
            if (user == null)
            {
                context.Response.StatusCode = 401;
                return;
            }

            string kind = (context.Request.QueryString["kind"] ?? "attendance").ToLowerInvariant();
            DateTime from = ParseDate(context.Request.QueryString["from"], PunchService.CurrentWorkDate().AddDays(-30));
            DateTime to = ParseDate(context.Request.QueryString["to"], PunchService.CurrentWorkDate());

            int? employeeId = ParseInt(context.Request.QueryString["employeeId"]);
            if (!user.CanSeeOthers) employeeId = user.EmployeeId;   // never widen an employee's own scope

            string csv = kind == "punches"
                ? BuildPunchCsv(employeeId, from, to, context.Request.QueryString["status"])
                : BuildAttendanceCsv(employeeId, from, to);

            string fileName = "remotepunch-" + kind + "-" +
                              from.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" +
                              to.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".csv";

            new AuditRepository().Write(user.EmployeeId, "Export",
                kind + " " + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".." +
                to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ClientInfo.FromRequest(context.Request));

            context.Response.Clear();
            context.Response.ContentType = "text/csv; charset=utf-8";
            context.Response.AppendHeader("Content-Disposition", "attachment; filename=\"" + fileName + "\"");
            // BOM so Excel opens UTF-8 names correctly.
            context.Response.BinaryWrite(Encoding.UTF8.GetPreamble());
            context.Response.Write(csv);
        }

        private static string BuildAttendanceCsv(int? employeeId, DateTime from, DateTime to)
        {
            List<AttendanceDay> rows = new PunchRepository().GetDailyAttendance(employeeId, from, to);

            StringBuilder csv = new StringBuilder();
            csv.AppendLine("Work date,Employee code,Employee,First in,Last out,Worked hours,Punches,Flagged,Late minutes");
            foreach (AttendanceDay row in rows)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Cell(row.WorkDateLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    Cell(row.EmployeeCode),
                    Cell(row.FullName),
                    Cell(row.FirstInLocal.HasValue ? row.FirstInLocal.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : ""),
                    Cell(row.LastOutLocal.HasValue ? row.LastOutLocal.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : ""),
                    Cell((row.WorkedMinutes / 60.0).ToString("F2", CultureInfo.InvariantCulture)),
                    Cell(row.PunchCount.ToString(CultureInfo.InvariantCulture)),
                    Cell(row.FlaggedCount.ToString(CultureInfo.InvariantCulture)),
                    Cell(row.LateMinutes.HasValue ? row.LateMinutes.Value.ToString(CultureInfo.InvariantCulture) : "")
                }));
            }
            return csv.ToString();
        }

        private static string BuildPunchCsv(int? employeeId, DateTime from, DateTime to, string status)
        {
            List<Punch> rows = new PunchRepository().Search(employeeId, from, to, status, null, false, 20000);

            StringBuilder csv = new StringBuilder();
            csv.AppendLine("Punch id,Work date,Local time,Employee code,Employee,Type,Status,Site,Distance (m)," +
                           "Inside geofence,Latitude,Longitude,Accuracy (m),Risk,Flags,Reject reason,Address," +
                           "IP address,Device,Note,Review status");
            foreach (Punch row in rows)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Cell(row.PunchId.ToString(CultureInfo.InvariantCulture)),
                    Cell(row.WorkDateLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    Cell(row.PunchTimeLocal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                    Cell(row.EmployeeCode),
                    Cell(row.EmployeeName),
                    Cell(row.PunchType),
                    Cell(row.Status),
                    Cell(row.SiteName),
                    Cell(row.DistanceMeters.HasValue ? Math.Round(row.DistanceMeters.Value).ToString("F0", CultureInfo.InvariantCulture) : ""),
                    Cell(row.IsWithinGeofence ? "yes" : "no"),
                    Cell(row.Latitude.ToString("F6", CultureInfo.InvariantCulture)),
                    Cell(row.Longitude.ToString("F6", CultureInfo.InvariantCulture)),
                    Cell(row.AccuracyMeters.HasValue ? Math.Round(row.AccuracyMeters.Value).ToString("F0", CultureInfo.InvariantCulture) : ""),
                    Cell(row.RiskScore.ToString(CultureInfo.InvariantCulture)),
                    Cell(row.FlagReasons),
                    Cell(row.RejectReason),
                    Cell(row.ResolvedAddress),
                    Cell(row.IpAddress),
                    Cell(row.DeviceFingerprint == null ? "" : row.DeviceFingerprint.Substring(0, 12)),
                    Cell(row.Note),
                    Cell(row.ReviewStatus)
                }));
            }
            return csv.ToString();
        }

        /// <summary>
        /// Quotes a CSV cell, and neutralises the leading characters spreadsheets
        /// treat as the start of a formula.
        /// </summary>
        private static string Cell(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            string text = value.Replace("\r", " ").Replace("\n", " ");
            if ("=+-@\t".IndexOf(text[0]) >= 0) text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private static DateTime ParseDate(string value, DateTime fallback)
        {
            DateTime parsed;
            if (!string.IsNullOrEmpty(value) &&
                DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed.Date;
            }
            return fallback.Date;
        }

        private static int? ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? (int?)parsed : null;
        }
    }
}
