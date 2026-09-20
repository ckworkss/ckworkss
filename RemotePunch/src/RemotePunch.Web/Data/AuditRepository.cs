using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using RemotePunch.Web.Core;

namespace RemotePunch.Web.Data
{
    /// <summary>Append-only trail of security-relevant events.</summary>
    public class AuditRepository
    {
        public void Write(int? employeeId, string eventType, string detail, ClientInfo client)
        {
            try
            {
                using (SqlConnection connection = Db.Open())
                using (SqlCommand command = Db.Command(connection,
                    "INSERT INTO dbo.AuditLog (EmployeeId, EventType, Detail, IpAddress, UserAgent) " +
                    "VALUES (@employeeId, @eventType, @detail, @ip, @userAgent)"))
                {
                    Db.Add(command, "@employeeId", SqlDbType.Int, employeeId.HasValue ? (object)employeeId.Value : null);
                    Db.Add(command, "@eventType", SqlDbType.NVarChar, 60, eventType);
                    Db.Add(command, "@detail", SqlDbType.NVarChar, 1000, ClientInfo.Truncate(detail, 1000));
                    Db.Add(command, "@ip", SqlDbType.NVarChar, 64, client == null ? null : client.IpAddress);
                    Db.Add(command, "@userAgent", SqlDbType.NVarChar, 500, client == null ? null : client.UserAgent);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // Auditing must never break the operation being audited.
                Trace.TraceError("Audit write failed: " + ex.Message);
            }
        }

        public List<AuditEntry> GetRecent(int maxRows, int? employeeId, string eventType)
        {
            List<AuditEntry> list = new List<AuditEntry>();
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT TOP (@maxRows) a.AuditId, a.EmployeeId, e.FullName, a.EventType, a.Detail, " +
                "       a.IpAddress, a.UserAgent, a.CreatedAtUtc " +
                "FROM dbo.AuditLog a LEFT JOIN dbo.Employees e ON e.EmployeeId = a.EmployeeId " +
                "WHERE (@employeeId IS NULL OR a.EmployeeId = @employeeId) " +
                "  AND (@eventType IS NULL OR a.EventType = @eventType) " +
                "ORDER BY a.AuditId DESC"))
            {
                Db.Add(command, "@maxRows", SqlDbType.Int, maxRows <= 0 ? 200 : maxRows);
                Db.Add(command, "@employeeId", SqlDbType.Int, employeeId.HasValue ? (object)employeeId.Value : null);
                Db.Add(command, "@eventType", SqlDbType.NVarChar, 60, string.IsNullOrEmpty(eventType) ? null : eventType);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new AuditEntry
                        {
                            AuditId = Db.GetLong(reader, "AuditId"),
                            EmployeeId = Db.GetNullableInt(reader, "EmployeeId"),
                            EmployeeName = Db.GetString(reader, "FullName"),
                            EventType = Db.GetString(reader, "EventType"),
                            Detail = Db.GetString(reader, "Detail"),
                            IpAddress = Db.GetString(reader, "IpAddress"),
                            UserAgent = Db.GetString(reader, "UserAgent"),
                            CreatedAtUtc = Db.GetDateTime(reader, "CreatedAtUtc")
                        });
                    }
                }
            }
            return list;
        }
    }

    public class AuditEntry
    {
        public long AuditId { get; set; }
        public int? EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string EventType { get; set; }
        public string Detail { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
