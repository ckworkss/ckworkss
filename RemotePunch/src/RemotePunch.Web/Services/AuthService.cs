using System;
using System.Globalization;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;
using RemotePunch.Web.Models;

namespace RemotePunch.Web.Services
{
    public class SignInResult
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public Employee Employee { get; set; }
    }

    /// <summary>Sign-in, lockout and password changes.</summary>
    public class AuthService
    {
        private readonly EmployeeRepository _employees = new EmployeeRepository();
        private readonly AuditRepository _audit = new AuditRepository();

        /// <summary>
        /// Verifies credentials. The message is deliberately the same for an
        /// unknown account and a wrong password, so the form cannot be used to
        /// enumerate employee codes.
        /// </summary>
        public SignInResult SignIn(string codeOrEmail, string password, ClientInfo client)
        {
            const string genericFailure = "Employee code or password is incorrect.";

            Employee employee = _employees.GetByCodeOrEmail(codeOrEmail);
            if (employee == null)
            {
                // Spend roughly the same time as a real verification would.
                byte[] dummyHash, dummySalt;
                int dummyIterations;
                PasswordHasher.CreateHash(password ?? "x", out dummyHash, out dummySalt, out dummyIterations);
                _audit.Write(null, "LoginFailed", "Unknown account: " + ClientInfo.Truncate(codeOrEmail, 100), client);
                return new SignInResult { Succeeded = false, Message = genericFailure };
            }

            if (!employee.IsActive)
            {
                _audit.Write(employee.EmployeeId, "LoginBlocked", "Account is inactive.", client);
                return new SignInResult { Succeeded = false, Message = "This account is no longer active." };
            }

            if (employee.IsLockedOut)
            {
                int minutes = (int)Math.Ceiling((employee.LockoutUntilUtc.Value - DateTime.UtcNow).TotalMinutes);
                _audit.Write(employee.EmployeeId, "LoginBlocked", "Account is locked out.", client);
                return new SignInResult
                {
                    Succeeded = false,
                    Message = "Too many failed attempts. Try again in " +
                              minutes.ToString(CultureInfo.InvariantCulture) + " minute(s)."
                };
            }

            bool valid = PasswordHasher.Verify(password, employee.PasswordHash, employee.PasswordSalt,
                                               employee.PasswordIterations);
            if (!valid)
            {
                _employees.RegisterFailedLogin(employee.EmployeeId, AppConfig.MaxFailedAttempts, AppConfig.LockoutMinutes);
                _audit.Write(employee.EmployeeId, "LoginFailed", "Wrong password.", client);
                return new SignInResult { Succeeded = false, Message = genericFailure };
            }

            _employees.RegisterSuccessfulLogin(employee.EmployeeId);
            _audit.Write(employee.EmployeeId, "LoginSucceeded", null, client);
            return new SignInResult { Succeeded = true, Employee = employee };
        }

        /// <summary>Changes a password after re-checking the current one.</summary>
        public string ChangePassword(int employeeId, string currentPassword, string newPassword, ClientInfo client)
        {
            Employee employee = _employees.GetById(employeeId);
            if (employee == null) return "Account not found.";

            if (!PasswordHasher.Verify(currentPassword, employee.PasswordHash, employee.PasswordSalt,
                                       employee.PasswordIterations))
            {
                _audit.Write(employeeId, "PasswordChangeFailed", "Current password did not match.", client);
                return "Your current password is incorrect.";
            }

            string problem;
            if (!PasswordHasher.IsStrongEnough(newPassword, out problem)) return problem;

            if (PasswordHasher.Verify(newPassword, employee.PasswordHash, employee.PasswordSalt,
                                      employee.PasswordIterations))
            {
                return "The new password must be different from the current one.";
            }

            byte[] hash, salt;
            int iterations;
            PasswordHasher.CreateHash(newPassword, out hash, out salt, out iterations);
            _employees.SetPassword(employeeId, hash, salt, iterations, false);
            _audit.Write(employeeId, "PasswordChanged", null, client);
            return null;
        }

        /// <summary>Administrator-driven reset; forces a change at next sign-in.</summary>
        public string ResetPassword(int targetEmployeeId, string newPassword, int actingAdminId, ClientInfo client)
        {
            string problem;
            if (!PasswordHasher.IsStrongEnough(newPassword, out problem)) return problem;

            byte[] hash, salt;
            int iterations;
            PasswordHasher.CreateHash(newPassword, out hash, out salt, out iterations);
            _employees.SetPassword(targetEmployeeId, hash, salt, iterations, true);
            _audit.Write(actingAdminId, "PasswordReset",
                "Reset password for employee #" + targetEmployeeId.ToString(CultureInfo.InvariantCulture), client);
            return null;
        }
    }
}
