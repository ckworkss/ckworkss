using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;
using RemotePunch.Web.Models;
using RemotePunch.Web.Services;

namespace RemotePunch.Web.Admin
{
    /// <summary>Create or edit one employee, their policy and their site access.</summary>
    public partial class EmployeeEditPage : Page
    {
        protected Literal litTitle, litMessage, litPasswordLabel;
        protected PlaceHolder phMessage;
        protected TextBox txtCode, txtName, txtEmail, txtPhone, txtShiftStart, txtShiftEnd, txtPassword;
        protected DropDownList ddlRole;
        protected CheckBox chkActive, chkRemote, chkSelfie;
        protected CheckBoxList chkSites;
        protected Button btnSave, btnUnlock;

        private readonly EmployeeRepository _employees = new EmployeeRepository();
        private readonly SiteRepository _sites = new SiteRepository();

        public string MessageClass { get; private set; }

        private int EmployeeId
        {
            get { return PageHelpers.ParseInt(Request.QueryString["id"]).GetValueOrDefault(); }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            AppUser.Require();
            MessageClass = "alert-bad";

            if (!IsPostBack)
            {
                BindSites();
                if (EmployeeId > 0) LoadEmployee();

                litTitle.Text = EmployeeId > 0 ? "Edit employee" : "Add employee";
                litPasswordLabel.Text = EmployeeId > 0 ? "Reset password (leave blank to keep)" : "Initial password";
                btnUnlock.Visible = EmployeeId > 0;
            }
        }

        private void BindSites()
        {
            chkSites.Items.Clear();
            foreach (Site site in _sites.GetAll(false))
            {
                chkSites.Items.Add(new ListItem(
                    site.Name + " (" + site.RadiusMeters.ToString(CultureInfo.InvariantCulture) + " m)",
                    site.SiteId.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private void LoadEmployee()
        {
            Employee employee = _employees.GetById(EmployeeId);
            if (employee == null)
            {
                Response.Redirect("~/Admin/Employees.aspx", true);
                return;
            }

            txtCode.Text = employee.EmployeeCode;
            txtName.Text = employee.FullName;
            txtEmail.Text = employee.Email;
            txtPhone.Text = employee.Phone;
            ddlRole.SelectedValue = employee.Role;
            chkActive.Checked = employee.IsActive;
            chkRemote.Checked = employee.AllowRemotePunch;
            chkSelfie.Checked = employee.RequireSelfie;

            if (employee.ShiftStartLocal.HasValue)
                txtShiftStart.Text = employee.ShiftStartLocal.Value.ToString("hh\\:mm", CultureInfo.InvariantCulture);
            if (employee.ShiftEndLocal.HasValue)
                txtShiftEnd.Text = employee.ShiftEndLocal.Value.ToString("hh\\:mm", CultureInfo.InvariantCulture);

            List<int> assigned = _employees.GetAssignedSiteIds(employee.EmployeeId);
            foreach (ListItem item in chkSites.Items)
            {
                item.Selected = assigned.Contains(int.Parse(item.Value, CultureInfo.InvariantCulture));
            }
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            AppUser actor = AppUser.Require();

            string code = txtCode.Text.Trim();
            string name = txtName.Text.Trim();
            string email = txtEmail.Text.Trim();

            if (code.Length == 0 || name.Length == 0 || email.Length == 0)
            {
                Show("Employee code, name and email are all required.", false);
                return;
            }
            if (_employees.CodeOrEmailTaken(code, email, EmployeeId))
            {
                Show("Another employee already uses that code or email.", false);
                return;
            }

            TimeSpan? shiftStart = ParseTime(txtShiftStart.Text);
            TimeSpan? shiftEnd = ParseTime(txtShiftEnd.Text);

            Employee employee = new Employee
            {
                EmployeeId = EmployeeId,
                EmployeeCode = code,
                FullName = name,
                Email = email,
                Phone = txtPhone.Text.Trim(),
                Role = ddlRole.SelectedValue,
                IsActive = chkActive.Checked,
                AllowRemotePunch = chkRemote.Checked,
                RequireSelfie = chkSelfie.Checked,
                ShiftStartLocal = shiftStart,
                ShiftEndLocal = shiftEnd
            };

            int employeeId = EmployeeId;
            if (employeeId > 0)
            {
                _employees.Update(employee);
            }
            else
            {
                if (txtPassword.Text.Length == 0)
                {
                    Show("Set an initial password for the new employee.", false);
                    return;
                }
                string weakness;
                if (!PasswordHasher.IsStrongEnough(txtPassword.Text, out weakness))
                {
                    Show(weakness, false);
                    return;
                }

                byte[] hash, salt;
                int iterations;
                PasswordHasher.CreateHash(txtPassword.Text, out hash, out salt, out iterations);
                employee.PasswordHash = hash;
                employee.PasswordSalt = salt;
                employee.PasswordIterations = iterations;
                employee.MustChangePassword = true;
                employeeId = _employees.Insert(employee);
            }

            // Site assignments.
            List<int> siteIds = new List<int>();
            foreach (ListItem item in chkSites.Items)
            {
                if (item.Selected) siteIds.Add(int.Parse(item.Value, CultureInfo.InvariantCulture));
            }
            _employees.ReplaceAssignedSites(employeeId, siteIds);

            // Optional password reset on an existing account.
            if (EmployeeId > 0 && txtPassword.Text.Length > 0)
            {
                string problem = new AuthService().ResetPassword(employeeId, txtPassword.Text, actor.EmployeeId,
                                                                 ClientInfo.FromRequest(Request));
                if (problem != null)
                {
                    Show(problem, false);
                    return;
                }
            }

            new AuditRepository().Write(actor.EmployeeId, EmployeeId > 0 ? "EmployeeUpdated" : "EmployeeCreated",
                code + " (" + name + ")", ClientInfo.FromRequest(Request));

            Response.Redirect("~/Admin/Employees.aspx", true);
        }

        protected void btnUnlock_Click(object sender, EventArgs e)
        {
            AppUser actor = AppUser.Require();
            if (EmployeeId <= 0) return;

            _employees.ClearLockout(EmployeeId);
            new AuditRepository().Write(actor.EmployeeId, "LockoutCleared",
                "Employee #" + EmployeeId.ToString(CultureInfo.InvariantCulture), ClientInfo.FromRequest(Request));
            Show("Lockout cleared.", true);
        }

        private static TimeSpan? ParseTime(string value)
        {
            TimeSpan parsed;
            if (string.IsNullOrWhiteSpace(value)) return null;
            return TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out parsed) ? (TimeSpan?)parsed : null;
        }

        private void Show(string message, bool good)
        {
            phMessage.Visible = true;
            MessageClass = good ? "alert-ok" : "alert-bad";
            litMessage.Text = Server.HtmlEncode(message);
        }
    }
}
