using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;
using RemotePunch.Web.Models;

namespace RemotePunch.Web
{
    /// <summary>
    /// Answers "why will this password not work" from inside the running
    /// application, using the same connection string, the same repository and
    /// the same hash verification as the sign-in form.
    ///
    /// Two gates keep it from being a back door: it is off unless Diag.Enabled
    /// is true in Web.config, and it only answers requests from the server
    /// itself. It reveals no password and no complete hash.
    /// </summary>
    public partial class DiagnosticsPage : Page
    {
        protected Literal litConnection, litResult, litEmployees;
        protected PlaceHolder phResult;
        protected TextBox txtCode, txtPassword;
        protected Button btnTest;

        private bool _enabled;

        protected void Page_Load(object sender, EventArgs e)
        {
            _enabled = AppConfig.GetBool("Diag.Enabled", false) && Request.IsLocal;
            if (!_enabled)
            {
                NotFound();
                return;
            }

            if (!IsPostBack)
            {
                ShowConnection();
                ShowEmployees();
            }
        }

        private void ShowConnection()
        {
            StringBuilder html = new StringBuilder();
            try
            {
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(Db.ConnectionString);
                Row(html, "Configured server", builder.DataSource);
                Row(html, "Configured database", builder.InitialCatalog);
                Row(html, "Authentication", builder.IntegratedSecurity
                    ? "Windows - connecting as the application pool identity"
                    : "SQL login '" + builder.UserID + "'");

                using (SqlConnection connection = Db.Open())
                using (SqlCommand command = Db.Command(connection,
                    "SELECT DB_NAME() AS CurrentDb, @@SERVERNAME AS ServerName, SUSER_SNAME() AS LoginName, " +
                    "(SELECT COUNT(*) FROM dbo.Employees) AS EmployeeCount"))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Row(html, "Actually connected to", Db.GetString(reader, "CurrentDb"), true);
                        Row(html, "On server", Db.GetString(reader, "ServerName"), true);
                        Row(html, "Connected as", Db.GetString(reader, "LoginName"));
                        Row(html, "Employees in that database",
                            Db.GetInt(reader, "EmployeeCount").ToString(CultureInfo.InvariantCulture), true);
                    }
                }
            }
            catch (Exception ex)
            {
                Row(html, "Connection failed", ex.GetType().Name + ": " + ex.Message);
            }
            litConnection.Text = html.ToString();
        }

        private void ShowEmployees()
        {
            StringBuilder html = new StringBuilder();
            html.Append("<table class=\"data\"><thead><tr><th>Id</th><th>Code</th><th>Name</th><th>Role</th>")
                .Append("<th>Active</th><th>Must change</th><th class=\"num\">Failed</th><th>Locked until</th>")
                .Append("<th class=\"num\">Hash bytes</th><th class=\"num\">Salt bytes</th><th class=\"num\">Iterations</th></tr></thead><tbody>");

            try
            {
                foreach (Employee employee in new EmployeeRepository().GetAll(true))
                {
                    bool goodHash = employee.PasswordHash != null && employee.PasswordHash.Length == 32;
                    bool goodSalt = employee.PasswordSalt != null && employee.PasswordSalt.Length == 32;

                    html.Append("<tr>")
                        .Append("<td>").Append(employee.EmployeeId).Append("</td>")
                        .Append("<td class=\"mono\">").Append(PageHelpers.Encode(employee.EmployeeCode)).Append("</td>")
                        .Append("<td>").Append(PageHelpers.Encode(employee.FullName)).Append("</td>")
                        .Append("<td>").Append(PageHelpers.Encode(employee.Role)).Append("</td>")
                        .Append("<td>").Append(employee.IsActive ? "yes" : "<strong>no</strong>").Append("</td>")
                        .Append("<td>").Append(employee.MustChangePassword ? "yes" : "no").Append("</td>")
                        .Append("<td class=\"num\">").Append(employee.FailedLoginCount).Append("</td>")
                        .Append("<td>").Append(employee.LockoutUntilUtc.HasValue
                            ? PageHelpers.Encode(employee.LockoutUntilUtc.Value.ToString("u", CultureInfo.InvariantCulture))
                            : "&mdash;").Append("</td>")
                        .Append(Cell(employee.PasswordHash == null ? "null" : employee.PasswordHash.Length.ToString(CultureInfo.InvariantCulture), goodHash))
                        .Append(Cell(employee.PasswordSalt == null ? "null" : employee.PasswordSalt.Length.ToString(CultureInfo.InvariantCulture), goodSalt))
                        .Append("<td class=\"num\">").Append(employee.PasswordIterations).Append("</td>")
                        .Append("</tr>");
                }
            }
            catch (Exception ex)
            {
                html.Append("<tr><td colspan=\"11\">Query failed: ")
                    .Append(PageHelpers.Encode(ex.Message)).Append("</td></tr>");
            }

            html.Append("</tbody></table>");
            litEmployees.Text = html.ToString();
        }

        protected void btnTest_Click(object sender, EventArgs e)
        {
            if (!_enabled)
            {
                NotFound();
                return;
            }

            ShowConnection();
            ShowEmployees();
            phResult.Visible = true;

            string typed = txtPassword.Text;
            StringBuilder html = new StringBuilder();

            // Proves whether the form actually delivers the password to the server.
            Row(html, "Password characters received", typed.Length.ToString(CultureInfo.InvariantCulture),
                typed.Length > 0);
            Row(html, "UTF-8 bytes received",
                Encoding.UTF8.GetByteCount(typed).ToString(CultureInfo.InvariantCulture));
            if (typed != typed.Trim())
            {
                Row(html, "Whitespace", "LEADING OR TRAILING WHITESPACE PRESENT", false);
            }

            Employee employee = new EmployeeRepository().GetByCodeOrEmail(txtCode.Text);
            if (employee == null)
            {
                Row(html, "Lookup", "NO employee matches '" + PageHelpers.Encode(txtCode.Text) +
                                    "' in this database", false);
                litResult.Text = html.ToString();
                return;
            }

            Row(html, "Lookup", "found employee #" +
                employee.EmployeeId.ToString(CultureInfo.InvariantCulture) + " (" +
                PageHelpers.Encode(employee.EmployeeCode) + ")", true);
            Row(html, "Stored hash length",
                (employee.PasswordHash == null ? "null" : employee.PasswordHash.Length + " bytes"),
                employee.PasswordHash != null && employee.PasswordHash.Length == 32);
            Row(html, "Stored salt length",
                (employee.PasswordSalt == null ? "null" : employee.PasswordSalt.Length + " bytes"),
                employee.PasswordSalt != null && employee.PasswordSalt.Length == 32);
            Row(html, "Iterations", employee.PasswordIterations.ToString(CultureInfo.InvariantCulture));

            // The first bytes of each are enough to see whether the derivation
            // agrees, without putting a usable hash on screen.
            if (employee.PasswordHash != null && employee.PasswordSalt != null && typed.Length > 0)
            {
                Row(html, "Stored hash starts with", Hex(employee.PasswordHash, 8));
                try
                {
                    byte[] derived = PasswordHasher.DeriveForDiagnostics(
                        typed, employee.PasswordSalt, employee.PasswordIterations, employee.PasswordHash.Length);
                    Row(html, "Derived hash starts with", Hex(derived, 8));
                }
                catch (Exception ex)
                {
                    Row(html, "Derivation failed", ex.GetType().Name + ": " + ex.Message, false);
                }
            }

            bool verified = PasswordHasher.Verify(typed, employee.PasswordHash,
                                                  employee.PasswordSalt, employee.PasswordIterations);
            Row(html, "PasswordHasher.Verify", verified ? "TRUE - this password is accepted"
                                                        : "FALSE - this password is rejected", verified);

            if (verified)
            {
                html.Append("<p class=\"alert alert-ok\">The stored hash is correct for this password. ")
                    .Append("If the sign-in form still refuses it, the problem is what reaches the form ")
                    .Append("(autofill, keyboard layout), not the database.</p>");
            }
            else if (!employee.IsActive)
            {
                html.Append("<p class=\"alert alert-bad\">This account is also inactive, which blocks sign-in ")
                    .Append("regardless of the password.</p>");
            }

            litResult.Text = html.ToString();
        }

        /// <summary>Answers exactly as a missing page would, revealing nothing.</summary>
        private void NotFound()
        {
            Response.Clear();
            Response.StatusCode = 404;
            Response.SuppressContent = true;
            Response.End();
        }

        // ---- rendering ---------------------------------------------------

        private static void Row(StringBuilder html, string label, string value)
        {
            Row(html, label, value, null);
        }

        private static void Row(StringBuilder html, string label, string value, bool? good)
        {
            string colour = !good.HasValue ? "" : (good.Value ? " style=\"color:#12855a\"" : " style=\"color:#b3261e\"");
            html.Append("<div class=\"readout-row\"><span class=\"k\">")
                .Append(PageHelpers.Encode(label))
                .Append("</span><span class=\"v\"").Append(colour).Append('>')
                .Append(PageHelpers.Encode(value))
                .Append("</span></div>");
        }

        private static string Cell(string value, bool good)
        {
            return "<td class=\"num\"" + (good ? "" : " style=\"color:#b3261e;font-weight:600\"") + ">" +
                   PageHelpers.Encode(value) + "</td>";
        }

        private static string Hex(byte[] bytes, int count)
        {
            if (bytes == null) return "null";
            int take = Math.Min(count, bytes.Length);
            StringBuilder hex = new StringBuilder();
            for (int i = 0; i < take; i++) hex.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
            if (take < bytes.Length) hex.Append('…');
            return hex.ToString();
        }
    }
}
