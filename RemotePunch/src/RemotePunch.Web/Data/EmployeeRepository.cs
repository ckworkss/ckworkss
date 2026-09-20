using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using RemotePunch.Web.Models;

namespace RemotePunch.Web.Data
{
    public class EmployeeRepository
    {
        private const string SelectColumns =
            "EmployeeId, EmployeeCode, FullName, Email, Phone, PasswordHash, PasswordSalt, PasswordIterations, " +
            "MustChangePassword, Role, IsActive, ShiftStartLocal, ShiftEndLocal, AllowRemotePunch, RequireSelfie, " +
            "FailedLoginCount, LockoutUntilUtc, LastLoginUtc, CreatedAtUtc";

        public Employee GetByCodeOrEmail(string codeOrEmail)
        {
            if (string.IsNullOrWhiteSpace(codeOrEmail)) return null;

            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT TOP 1 " + SelectColumns + " FROM dbo.Employees " +
                "WHERE EmployeeCode = @key OR Email = @key"))
            {
                Db.Add(command, "@key", SqlDbType.NVarChar, 200, codeOrEmail.Trim());
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        public Employee GetById(int employeeId)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT " + SelectColumns + " FROM dbo.Employees WHERE EmployeeId = @id"))
            {
                Db.Add(command, "@id", SqlDbType.Int, employeeId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        public List<Employee> GetAll(bool includeInactive)
        {
            List<Employee> list = new List<Employee>();
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT " + SelectColumns + " FROM dbo.Employees " +
                "WHERE (@all = 1 OR IsActive = 1) ORDER BY FullName"))
            {
                Db.Add(command, "@all", SqlDbType.Bit, includeInactive);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) list.Add(Map(reader));
                }
            }
            return list;
        }

        public int Insert(Employee employee)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "INSERT INTO dbo.Employees " +
                "(EmployeeCode, FullName, Email, Phone, PasswordHash, PasswordSalt, PasswordIterations, " +
                " MustChangePassword, Role, IsActive, ShiftStartLocal, ShiftEndLocal, AllowRemotePunch, RequireSelfie) " +
                "VALUES (@code, @name, @email, @phone, @hash, @salt, @iter, @mustChange, @role, @active, " +
                "        @shiftStart, @shiftEnd, @remote, @selfie); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);"))
            {
                BindEditable(command, employee);
                Db.Add(command, "@hash", SqlDbType.VarBinary, 64, employee.PasswordHash);
                Db.Add(command, "@salt", SqlDbType.VarBinary, 32, employee.PasswordSalt);
                Db.Add(command, "@iter", SqlDbType.Int, employee.PasswordIterations);
                Db.Add(command, "@mustChange", SqlDbType.Bit, employee.MustChangePassword);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public void Update(Employee employee)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "UPDATE dbo.Employees SET EmployeeCode = @code, FullName = @name, Email = @email, Phone = @phone, " +
                "Role = @role, IsActive = @active, ShiftStartLocal = @shiftStart, ShiftEndLocal = @shiftEnd, " +
                "AllowRemotePunch = @remote, RequireSelfie = @selfie WHERE EmployeeId = @id"))
            {
                BindEditable(command, employee);
                Db.Add(command, "@id", SqlDbType.Int, employee.EmployeeId);
                command.ExecuteNonQuery();
            }
        }

        private static void BindEditable(SqlCommand command, Employee employee)
        {
            Db.Add(command, "@code", SqlDbType.NVarChar, 40, employee.EmployeeCode);
            Db.Add(command, "@name", SqlDbType.NVarChar, 160, employee.FullName);
            Db.Add(command, "@email", SqlDbType.NVarChar, 200, employee.Email);
            Db.Add(command, "@phone", SqlDbType.NVarChar, 40, employee.Phone);
            Db.Add(command, "@role", SqlDbType.NVarChar, 20, employee.Role);
            Db.Add(command, "@active", SqlDbType.Bit, employee.IsActive);
            Db.Add(command, "@shiftStart", SqlDbType.Time, employee.ShiftStartLocal);
            Db.Add(command, "@shiftEnd", SqlDbType.Time, employee.ShiftEndLocal);
            Db.Add(command, "@remote", SqlDbType.Bit, employee.AllowRemotePunch);
            Db.Add(command, "@selfie", SqlDbType.Bit, employee.RequireSelfie);
        }

        public void SetPassword(int employeeId, byte[] hash, byte[] salt, int iterations, bool mustChange)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "UPDATE dbo.Employees SET PasswordHash = @hash, PasswordSalt = @salt, PasswordIterations = @iter, " +
                "MustChangePassword = @mustChange, FailedLoginCount = 0, LockoutUntilUtc = NULL " +
                "WHERE EmployeeId = @id"))
            {
                Db.Add(command, "@hash", SqlDbType.VarBinary, 64, hash);
                Db.Add(command, "@salt", SqlDbType.VarBinary, 32, salt);
                Db.Add(command, "@iter", SqlDbType.Int, iterations);
                Db.Add(command, "@mustChange", SqlDbType.Bit, mustChange);
                Db.Add(command, "@id", SqlDbType.Int, employeeId);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>Records a failed sign-in and locks the account once the limit is hit.</summary>
        public void RegisterFailedLogin(int employeeId, int maxAttempts, int lockoutMinutes)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "UPDATE dbo.Employees " +
                "SET FailedLoginCount = FailedLoginCount + 1, " +
                "    LockoutUntilUtc = CASE WHEN FailedLoginCount + 1 >= @max " +
                "                           THEN DATEADD(MINUTE, @minutes, SYSUTCDATETIME()) ELSE LockoutUntilUtc END " +
                "WHERE EmployeeId = @id"))
            {
                Db.Add(command, "@max", SqlDbType.Int, maxAttempts);
                Db.Add(command, "@minutes", SqlDbType.Int, lockoutMinutes);
                Db.Add(command, "@id", SqlDbType.Int, employeeId);
                command.ExecuteNonQuery();
            }
        }

        public void RegisterSuccessfulLogin(int employeeId)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "UPDATE dbo.Employees SET FailedLoginCount = 0, LockoutUntilUtc = NULL, " +
                "LastLoginUtc = SYSUTCDATETIME() WHERE EmployeeId = @id"))
            {
                Db.Add(command, "@id", SqlDbType.Int, employeeId);
                command.ExecuteNonQuery();
            }
        }

        public void ClearLockout(int employeeId)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "UPDATE dbo.Employees SET FailedLoginCount = 0, LockoutUntilUtc = NULL WHERE EmployeeId = @id"))
            {
                Db.Add(command, "@id", SqlDbType.Int, employeeId);
                command.ExecuteNonQuery();
            }
        }

        public bool CodeOrEmailTaken(string code, string email, int exceptEmployeeId)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT COUNT(*) FROM dbo.Employees " +
                "WHERE (EmployeeCode = @code OR Email = @email) AND EmployeeId <> @id"))
            {
                Db.Add(command, "@code", SqlDbType.NVarChar, 40, code);
                Db.Add(command, "@email", SqlDbType.NVarChar, 200, email);
                Db.Add(command, "@id", SqlDbType.Int, exceptEmployeeId);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        // ---- site assignment --------------------------------------------

        public List<int> GetAssignedSiteIds(int employeeId)
        {
            List<int> ids = new List<int>();
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT SiteId FROM dbo.EmployeeSites WHERE EmployeeId = @id"))
            {
                Db.Add(command, "@id", SqlDbType.Int, employeeId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) ids.Add(reader.GetInt32(0));
                }
            }
            return ids;
        }

        public void ReplaceAssignedSites(int employeeId, IEnumerable<int> siteIds)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlTransaction transaction = connection.BeginTransaction())
            {
                using (SqlCommand delete = new SqlCommand(
                    "DELETE FROM dbo.EmployeeSites WHERE EmployeeId = @id", connection, transaction))
                {
                    delete.Parameters.Add("@id", SqlDbType.Int).Value = employeeId;
                    delete.ExecuteNonQuery();
                }

                foreach (int siteId in siteIds)
                {
                    using (SqlCommand insert = new SqlCommand(
                        "INSERT INTO dbo.EmployeeSites (EmployeeId, SiteId) VALUES (@emp, @site)",
                        connection, transaction))
                    {
                        insert.Parameters.Add("@emp", SqlDbType.Int).Value = employeeId;
                        insert.Parameters.Add("@site", SqlDbType.Int).Value = siteId;
                        insert.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }

        private static Employee Map(IDataRecord row)
        {
            return new Employee
            {
                EmployeeId = Db.GetInt(row, "EmployeeId"),
                EmployeeCode = Db.GetString(row, "EmployeeCode"),
                FullName = Db.GetString(row, "FullName"),
                Email = Db.GetString(row, "Email"),
                Phone = Db.GetString(row, "Phone"),
                PasswordHash = Db.GetBytes(row, "PasswordHash"),
                PasswordSalt = Db.GetBytes(row, "PasswordSalt"),
                PasswordIterations = Db.GetInt(row, "PasswordIterations"),
                MustChangePassword = Db.GetBool(row, "MustChangePassword"),
                Role = Db.GetString(row, "Role"),
                IsActive = Db.GetBool(row, "IsActive"),
                ShiftStartLocal = Db.GetNullableTime(row, "ShiftStartLocal"),
                ShiftEndLocal = Db.GetNullableTime(row, "ShiftEndLocal"),
                AllowRemotePunch = Db.GetBool(row, "AllowRemotePunch"),
                RequireSelfie = Db.GetBool(row, "RequireSelfie"),
                FailedLoginCount = Db.GetInt(row, "FailedLoginCount"),
                LockoutUntilUtc = Db.GetNullableDateTime(row, "LockoutUntilUtc"),
                LastLoginUtc = Db.GetNullableDateTime(row, "LastLoginUtc"),
                CreatedAtUtc = Db.GetDateTime(row, "CreatedAtUtc")
            };
        }
    }
}
