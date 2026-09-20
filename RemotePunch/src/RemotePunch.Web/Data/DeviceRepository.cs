using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using RemotePunch.Web.Models;

namespace RemotePunch.Web.Data
{
    public class DeviceRepository
    {
        /// <summary>
        /// Records the device this punch came from and returns its stored state.
        /// The first device an employee ever uses can be trusted automatically
        /// (Fraud.AutoTrustFirstDevice); later ones start untrusted.
        /// </summary>
        public Device Upsert(int employeeId, string fingerprint, string label, bool autoTrustFirst)
        {
            if (string.IsNullOrEmpty(fingerprint)) return null;

            using (SqlConnection connection = Db.Open())
            {
                using (SqlCommand command = Db.Command(connection,
                    "MERGE dbo.Devices AS target " +
                    "USING (SELECT @employeeId AS EmployeeId, @fingerprint AS Fingerprint) AS source " +
                    "   ON target.EmployeeId = source.EmployeeId AND target.Fingerprint = source.Fingerprint " +
                    "WHEN MATCHED THEN " +
                    "   UPDATE SET LastSeenUtc = SYSUTCDATETIME(), PunchCount = target.PunchCount + 1, " +
                    "              Label = COALESCE(@label, target.Label) " +
                    "WHEN NOT MATCHED THEN " +
                    "   INSERT (EmployeeId, Fingerprint, Label, IsTrusted, PunchCount) " +
                    "   VALUES (@employeeId, @fingerprint, @label, " +
                    "           CASE WHEN @autoTrust = 1 AND NOT EXISTS " +
                    "                     (SELECT 1 FROM dbo.Devices d WHERE d.EmployeeId = @employeeId) " +
                    "                THEN 1 ELSE 0 END, 1);"))
                {
                    Db.Add(command, "@employeeId", SqlDbType.Int, employeeId);
                    Db.Add(command, "@fingerprint", SqlDbType.Char, 64, fingerprint);
                    Db.Add(command, "@label", SqlDbType.NVarChar, 200, label);
                    Db.Add(command, "@autoTrust", SqlDbType.Bit, autoTrustFirst);
                    command.ExecuteNonQuery();
                }

                using (SqlCommand command = Db.Command(connection,
                    "SELECT DeviceId, EmployeeId, Fingerprint, Label, IsTrusted, IsBlocked, " +
                    "       FirstSeenUtc, LastSeenUtc, PunchCount " +
                    "FROM dbo.Devices WHERE EmployeeId = @employeeId AND Fingerprint = @fingerprint"))
                {
                    Db.Add(command, "@employeeId", SqlDbType.Int, employeeId);
                    Db.Add(command, "@fingerprint", SqlDbType.Char, 64, fingerprint);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? Map(reader) : null;
                    }
                }
            }
        }

        public List<Device> GetAll(int? employeeId)
        {
            List<Device> list = new List<Device>();
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT d.DeviceId, d.EmployeeId, e.FullName AS EmployeeName, d.Fingerprint, d.Label, " +
                "       d.IsTrusted, d.IsBlocked, d.FirstSeenUtc, d.LastSeenUtc, d.PunchCount " +
                "FROM dbo.Devices d INNER JOIN dbo.Employees e ON e.EmployeeId = d.EmployeeId " +
                "WHERE (@employeeId IS NULL OR d.EmployeeId = @employeeId) " +
                "ORDER BY d.LastSeenUtc DESC"))
            {
                Db.Add(command, "@employeeId", SqlDbType.Int, employeeId.HasValue ? (object)employeeId.Value : null);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Device device = Map(reader);
                        device.EmployeeName = Db.GetString(reader, "EmployeeName");
                        list.Add(device);
                    }
                }
            }
            return list;
        }

        public Device GetById(int deviceId)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT d.DeviceId, d.EmployeeId, e.FullName AS EmployeeName, d.Fingerprint, d.Label, " +
                "       d.IsTrusted, d.IsBlocked, d.FirstSeenUtc, d.LastSeenUtc, d.PunchCount " +
                "FROM dbo.Devices d INNER JOIN dbo.Employees e ON e.EmployeeId = d.EmployeeId " +
                "WHERE d.DeviceId = @id"))
            {
                Db.Add(command, "@id", SqlDbType.Int, deviceId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    Device device = Map(reader);
                    device.EmployeeName = Db.GetString(reader, "EmployeeName");
                    return device;
                }
            }
        }

        public void SetFlags(int deviceId, bool isTrusted, bool isBlocked)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "UPDATE dbo.Devices SET IsTrusted = @trusted, IsBlocked = @blocked WHERE DeviceId = @id"))
            {
                Db.Add(command, "@trusted", SqlDbType.Bit, isTrusted);
                Db.Add(command, "@blocked", SqlDbType.Bit, isBlocked);
                Db.Add(command, "@id", SqlDbType.Int, deviceId);
                command.ExecuteNonQuery();
            }
        }

        public void Delete(int deviceId)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection, "DELETE FROM dbo.Devices WHERE DeviceId = @id"))
            {
                Db.Add(command, "@id", SqlDbType.Int, deviceId);
                command.ExecuteNonQuery();
            }
        }

        private static Device Map(IDataRecord row)
        {
            return new Device
            {
                DeviceId = Db.GetInt(row, "DeviceId"),
                EmployeeId = Db.GetInt(row, "EmployeeId"),
                Fingerprint = Db.GetString(row, "Fingerprint"),
                Label = Db.GetString(row, "Label"),
                IsTrusted = Db.GetBool(row, "IsTrusted"),
                IsBlocked = Db.GetBool(row, "IsBlocked"),
                FirstSeenUtc = Db.GetDateTime(row, "FirstSeenUtc"),
                LastSeenUtc = Db.GetDateTime(row, "LastSeenUtc"),
                PunchCount = Db.GetInt(row, "PunchCount")
            };
        }
    }
}
