using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using RemotePunch.Web.Models;

namespace RemotePunch.Web.Data
{
    public class PunchRepository
    {
        private const string SelectColumns =
            "p.PunchId, p.EmployeeId, e.EmployeeCode, e.FullName AS EmployeeName, p.PunchType, p.Status, " +
            "p.PunchTimeUtc, p.PunchTimeLocal, p.WorkDateLocal, p.Latitude, p.Longitude, p.AccuracyMeters, " +
            "p.Altitude, p.AltitudeAccuracy, p.Speed, p.Heading, p.PositionTimestamp, p.SiteId, s.Name AS SiteName, " +
            "p.DistanceMeters, p.IsWithinGeofence, p.ResolvedAddress, p.RiskScore, p.FlagReasons, p.RejectReason, " +
            "p.IpAddress, p.UserAgent, p.DeviceFingerprint, p.SelfiePath, p.ClientRequestId, p.Note, " +
            "p.ReviewStatus, p.ReviewedByEmpId, r.FullName AS ReviewedByName, p.ReviewedAtUtc, p.ReviewNote, p.CreatedAtUtc";

        private const string FromClause =
            "FROM dbo.Punches p " +
            "INNER JOIN dbo.Employees e ON e.EmployeeId = p.EmployeeId " +
            "LEFT JOIN dbo.Sites s ON s.SiteId = p.SiteId " +
            "LEFT JOIN dbo.Employees r ON r.EmployeeId = p.ReviewedByEmpId ";

        public long Insert(Punch punch)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "INSERT INTO dbo.Punches " +
                "(EmployeeId, PunchType, Status, PunchTimeUtc, PunchTimeLocal, WorkDateLocal, Latitude, Longitude, " +
                " AccuracyMeters, Altitude, AltitudeAccuracy, Speed, Heading, PositionTimestamp, SiteId, DistanceMeters, " +
                " IsWithinGeofence, ResolvedAddress, RiskScore, FlagReasons, RejectReason, IpAddress, UserAgent, " +
                " DeviceFingerprint, SelfiePath, ClientRequestId, Note, ReviewStatus) " +
                "VALUES (@employeeId, @punchType, @status, @timeUtc, @timeLocal, @workDate, @lat, @lng, " +
                "        @accuracy, @altitude, @altAccuracy, @speed, @heading, @posTimestamp, @siteId, @distance, " +
                "        @within, @address, @risk, @flags, @rejectReason, @ip, @userAgent, " +
                "        @fingerprint, @selfie, @requestId, @note, @reviewStatus); " +
                "SELECT CAST(SCOPE_IDENTITY() AS BIGINT);"))
            {
                Db.Add(command, "@employeeId", SqlDbType.Int, punch.EmployeeId);
                Db.Add(command, "@punchType", SqlDbType.NVarChar, 3, punch.PunchType);
                Db.Add(command, "@status", SqlDbType.NVarChar, 10, punch.Status);
                Db.Add(command, "@timeUtc", SqlDbType.DateTime2, punch.PunchTimeUtc);
                Db.Add(command, "@timeLocal", SqlDbType.DateTime2, punch.PunchTimeLocal);
                Db.Add(command, "@workDate", SqlDbType.Date, punch.WorkDateLocal.Date);
                Db.AddDecimal(command, "@lat", punch.Latitude, 9, 6);
                Db.AddDecimal(command, "@lng", punch.Longitude, 9, 6);
                Db.Add(command, "@accuracy", SqlDbType.Float, punch.AccuracyMeters);
                Db.Add(command, "@altitude", SqlDbType.Float, punch.Altitude);
                Db.Add(command, "@altAccuracy", SqlDbType.Float, punch.AltitudeAccuracy);
                Db.Add(command, "@speed", SqlDbType.Float, punch.Speed);
                Db.Add(command, "@heading", SqlDbType.Float, punch.Heading);
                Db.Add(command, "@posTimestamp", SqlDbType.DateTime2, punch.PositionTimestamp);
                Db.Add(command, "@siteId", SqlDbType.Int, punch.SiteId);
                Db.Add(command, "@distance", SqlDbType.Float, punch.DistanceMeters);
                Db.Add(command, "@within", SqlDbType.Bit, punch.IsWithinGeofence);
                Db.Add(command, "@address", SqlDbType.NVarChar, 400, punch.ResolvedAddress);
                Db.Add(command, "@risk", SqlDbType.Int, punch.RiskScore);
                Db.Add(command, "@flags", SqlDbType.NVarChar, 1000, punch.FlagReasons);
                Db.Add(command, "@rejectReason", SqlDbType.NVarChar, 400, punch.RejectReason);
                Db.Add(command, "@ip", SqlDbType.NVarChar, 64, punch.IpAddress);
                Db.Add(command, "@userAgent", SqlDbType.NVarChar, 500, punch.UserAgent);
                Db.Add(command, "@fingerprint", SqlDbType.Char, 64, punch.DeviceFingerprint);
                Db.Add(command, "@selfie", SqlDbType.NVarChar, 300, punch.SelfiePath);
                Db.Add(command, "@requestId", SqlDbType.NVarChar, 64, punch.ClientRequestId);
                Db.Add(command, "@note", SqlDbType.NVarChar, 500, punch.Note);
                Db.Add(command, "@reviewStatus", SqlDbType.NVarChar, 10, punch.ReviewStatus ?? "None");
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        public Punch GetById(long punchId)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT " + SelectColumns + " " + FromClause + "WHERE p.PunchId = @id"))
            {
                Db.Add(command, "@id", SqlDbType.BigInt, punchId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        /// <summary>Supports the idempotency key: a retried request returns its first result.</summary>
        public Punch GetByClientRequestId(int employeeId, string clientRequestId)
        {
            if (string.IsNullOrEmpty(clientRequestId)) return null;

            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT " + SelectColumns + " " + FromClause +
                "WHERE p.EmployeeId = @employeeId AND p.ClientRequestId = @requestId"))
            {
                Db.Add(command, "@employeeId", SqlDbType.Int, employeeId);
                Db.Add(command, "@requestId", SqlDbType.NVarChar, 64, clientRequestId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        /// <summary>Most recent non-rejected punch, used to decide IN vs OUT and to spot impossible travel.</summary>
        public Punch GetLastAcceptedPunch(int employeeId)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT TOP 1 " + SelectColumns + " " + FromClause +
                "WHERE p.EmployeeId = @employeeId AND p.Status <> 'Rejected' " +
                "ORDER BY p.PunchTimeUtc DESC, p.PunchId DESC"))
            {
                Db.Add(command, "@employeeId", SqlDbType.Int, employeeId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        public List<Punch> GetForEmployeeAndDate(int employeeId, DateTime workDateLocal)
        {
            List<Punch> list = new List<Punch>();
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT " + SelectColumns + " " + FromClause +
                "WHERE p.EmployeeId = @employeeId AND p.WorkDateLocal = @workDate " +
                "ORDER BY p.PunchTimeUtc"))
            {
                Db.Add(command, "@employeeId", SqlDbType.Int, employeeId);
                Db.Add(command, "@workDate", SqlDbType.Date, workDateLocal.Date);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) list.Add(Map(reader));
                }
            }
            return list;
        }

        /// <summary>
        /// Filtered punch search for the reports screens. Every filter is
        /// optional; all of them are parameterised.
        /// </summary>
        public List<Punch> Search(int? employeeId, DateTime? fromLocal, DateTime? toLocal,
                                  string status, int? siteId, bool flaggedOnly, int maxRows)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("SELECT TOP (@maxRows) ").Append(SelectColumns).Append(' ').Append(FromClause).Append("WHERE 1 = 1 ");

            if (employeeId.HasValue) sql.Append("AND p.EmployeeId = @employeeId ");
            if (fromLocal.HasValue) sql.Append("AND p.WorkDateLocal >= @fromDate ");
            if (toLocal.HasValue) sql.Append("AND p.WorkDateLocal <= @toDate ");
            if (!string.IsNullOrEmpty(status)) sql.Append("AND p.Status = @status ");
            if (siteId.HasValue) sql.Append("AND p.SiteId = @siteId ");
            if (flaggedOnly) sql.Append("AND (p.Status = 'Flagged' OR p.ReviewStatus = 'Pending') ");

            sql.Append("ORDER BY p.PunchTimeUtc DESC, p.PunchId DESC");

            List<Punch> list = new List<Punch>();
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection, sql.ToString()))
            {
                Db.Add(command, "@maxRows", SqlDbType.Int, maxRows <= 0 ? 500 : maxRows);
                if (employeeId.HasValue) Db.Add(command, "@employeeId", SqlDbType.Int, employeeId.Value);
                if (fromLocal.HasValue) Db.Add(command, "@fromDate", SqlDbType.Date, fromLocal.Value.Date);
                if (toLocal.HasValue) Db.Add(command, "@toDate", SqlDbType.Date, toLocal.Value.Date);
                if (!string.IsNullOrEmpty(status)) Db.Add(command, "@status", SqlDbType.NVarChar, 10, status);
                if (siteId.HasValue) Db.Add(command, "@siteId", SqlDbType.Int, siteId.Value);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) list.Add(Map(reader));
                }
            }
            return list;
        }

        public List<AttendanceDay> GetDailyAttendance(int? employeeId, DateTime fromLocal, DateTime toLocal)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("SELECT EmployeeId, EmployeeCode, FullName, WorkDateLocal, FirstInLocal, LastOutLocal, ");
            sql.Append("WorkedMinutes, PunchCount, FlaggedCount, LateMinutes FROM dbo.vw_DailyAttendance ");
            sql.Append("WHERE WorkDateLocal BETWEEN @fromDate AND @toDate ");
            if (employeeId.HasValue) sql.Append("AND EmployeeId = @employeeId ");
            sql.Append("ORDER BY WorkDateLocal DESC, FullName");

            List<AttendanceDay> list = new List<AttendanceDay>();
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection, sql.ToString()))
            {
                Db.Add(command, "@fromDate", SqlDbType.Date, fromLocal.Date);
                Db.Add(command, "@toDate", SqlDbType.Date, toLocal.Date);
                if (employeeId.HasValue) Db.Add(command, "@employeeId", SqlDbType.Int, employeeId.Value);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new AttendanceDay
                        {
                            EmployeeId = Db.GetInt(reader, "EmployeeId"),
                            EmployeeCode = Db.GetString(reader, "EmployeeCode"),
                            FullName = Db.GetString(reader, "FullName"),
                            WorkDateLocal = Db.GetDateTime(reader, "WorkDateLocal"),
                            FirstInLocal = Db.GetNullableDateTime(reader, "FirstInLocal"),
                            LastOutLocal = Db.GetNullableDateTime(reader, "LastOutLocal"),
                            WorkedMinutes = Db.GetInt(reader, "WorkedMinutes"),
                            PunchCount = Db.GetInt(reader, "PunchCount"),
                            FlaggedCount = Db.GetInt(reader, "FlaggedCount"),
                            LateMinutes = Db.GetNullableInt(reader, "LateMinutes")
                        });
                    }
                }
            }
            return list;
        }

        public void SetReview(long punchId, string reviewStatus, int reviewerEmployeeId, string note)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "UPDATE dbo.Punches SET ReviewStatus = @review, ReviewedByEmpId = @reviewer, " +
                "ReviewedAtUtc = SYSUTCDATETIME(), ReviewNote = @note, " +
                "Status = CASE WHEN @review = 'Approved' THEN 'Accepted' ELSE Status END " +
                "WHERE PunchId = @id"))
            {
                Db.Add(command, "@review", SqlDbType.NVarChar, 10, reviewStatus);
                Db.Add(command, "@reviewer", SqlDbType.Int, reviewerEmployeeId);
                Db.Add(command, "@note", SqlDbType.NVarChar, 400, note);
                Db.Add(command, "@id", SqlDbType.BigInt, punchId);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>Headline numbers for the admin dashboard, for one local work date.</summary>
        public Dictionary<string, int> GetDashboardCounts(DateTime workDateLocal)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT " +
                "  (SELECT COUNT(DISTINCT EmployeeId) FROM dbo.Punches " +
                "     WHERE WorkDateLocal = @workDate AND Status <> 'Rejected') AS PresentCount, " +
                "  (SELECT COUNT(*) FROM dbo.Punches WHERE WorkDateLocal = @workDate) AS PunchCount, " +
                "  (SELECT COUNT(*) FROM dbo.Punches WHERE WorkDateLocal = @workDate AND Status = 'Flagged') AS FlaggedCount, " +
                "  (SELECT COUNT(*) FROM dbo.Punches WHERE WorkDateLocal = @workDate AND Status = 'Rejected') AS RejectedCount, " +
                "  (SELECT COUNT(*) FROM dbo.Punches WHERE ReviewStatus = 'Pending') AS PendingReviewCount, " +
                "  (SELECT COUNT(*) FROM dbo.Employees WHERE IsActive = 1) AS ActiveEmployeeCount, " +
                "  (SELECT COUNT(*) FROM dbo.Sites WHERE IsActive = 1) AS ActiveSiteCount"))
            {
                Db.Add(command, "@workDate", SqlDbType.Date, workDateLocal.Date);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            counts[reader.GetName(i)] = reader.IsDBNull(i) ? 0 : Convert.ToInt32(reader.GetValue(i));
                        }
                    }
                }
            }
            return counts;
        }

        private static Punch Map(IDataRecord row)
        {
            return new Punch
            {
                PunchId = Db.GetLong(row, "PunchId"),
                EmployeeId = Db.GetInt(row, "EmployeeId"),
                EmployeeCode = Db.GetString(row, "EmployeeCode"),
                EmployeeName = Db.GetString(row, "EmployeeName"),
                PunchType = Db.GetString(row, "PunchType"),
                Status = Db.GetString(row, "Status"),
                PunchTimeUtc = Db.GetDateTime(row, "PunchTimeUtc"),
                PunchTimeLocal = Db.GetDateTime(row, "PunchTimeLocal"),
                WorkDateLocal = Db.GetDateTime(row, "WorkDateLocal"),
                Latitude = Db.GetDouble(row, "Latitude"),
                Longitude = Db.GetDouble(row, "Longitude"),
                AccuracyMeters = Db.GetNullableDouble(row, "AccuracyMeters"),
                Altitude = Db.GetNullableDouble(row, "Altitude"),
                AltitudeAccuracy = Db.GetNullableDouble(row, "AltitudeAccuracy"),
                Speed = Db.GetNullableDouble(row, "Speed"),
                Heading = Db.GetNullableDouble(row, "Heading"),
                PositionTimestamp = Db.GetNullableDateTime(row, "PositionTimestamp"),
                SiteId = Db.GetNullableInt(row, "SiteId"),
                SiteName = Db.GetString(row, "SiteName"),
                DistanceMeters = Db.GetNullableDouble(row, "DistanceMeters"),
                IsWithinGeofence = Db.GetBool(row, "IsWithinGeofence"),
                ResolvedAddress = Db.GetString(row, "ResolvedAddress"),
                RiskScore = Db.GetInt(row, "RiskScore"),
                FlagReasons = Db.GetString(row, "FlagReasons"),
                RejectReason = Db.GetString(row, "RejectReason"),
                IpAddress = Db.GetString(row, "IpAddress"),
                UserAgent = Db.GetString(row, "UserAgent"),
                DeviceFingerprint = Db.GetString(row, "DeviceFingerprint"),
                SelfiePath = Db.GetString(row, "SelfiePath"),
                ClientRequestId = Db.GetString(row, "ClientRequestId"),
                Note = Db.GetString(row, "Note"),
                ReviewStatus = Db.GetString(row, "ReviewStatus"),
                ReviewedByEmpId = Db.GetNullableInt(row, "ReviewedByEmpId"),
                ReviewedByName = Db.GetString(row, "ReviewedByName"),
                ReviewedAtUtc = Db.GetNullableDateTime(row, "ReviewedAtUtc"),
                ReviewNote = Db.GetString(row, "ReviewNote"),
                CreatedAtUtc = Db.GetDateTime(row, "CreatedAtUtc")
            };
        }
    }
}
