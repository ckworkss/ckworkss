using System;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;
using RemotePunch.Web.Models;

namespace RemotePunch.Web.Admin
{
    public partial class EmployeesPage : Page
    {
        protected CheckBox chkIncludeInactive;
        protected Repeater rptEmployees;

        protected void Page_Load(object sender, EventArgs e)
        {
            AppUser.Require();
            if (!IsPostBack) Bind();
        }

        protected void chkIncludeInactive_CheckedChanged(object sender, EventArgs e)
        {
            Bind();
        }

        private void Bind()
        {
            rptEmployees.DataSource = new EmployeeRepository().GetAll(chkIncludeInactive.Checked);
            rptEmployees.DataBind();
        }

        protected string YesNo(object value)
        {
            bool flag = value != null && value != DBNull.Value && Convert.ToBoolean(value);
            return flag ? "<span class=\"badge badge-ok\">yes</span>" : "<span class=\"badge badge-muted\">no</span>";
        }

        protected string StatusBadge(object dataItem)
        {
            Employee employee = dataItem as Employee;
            if (employee == null) return string.Empty;
            if (!employee.IsActive) return "<span class=\"badge badge-bad\">inactive</span>";
            if (employee.IsLockedOut) return "<span class=\"badge badge-warn\">locked</span>";
            if (employee.MustChangePassword) return "<span class=\"badge badge-warn\">new password due</span>";
            return "<span class=\"badge badge-ok\">active</span>";
        }

        protected string ShiftText(object dataItem)
        {
            Employee employee = dataItem as Employee;
            if (employee == null || !employee.ShiftStartLocal.HasValue || !employee.ShiftEndLocal.HasValue)
                return "&mdash;";

            return employee.ShiftStartLocal.Value.ToString("hh\\:mm", CultureInfo.InvariantCulture) + " - " +
                   employee.ShiftEndLocal.Value.ToString("hh\\:mm", CultureInfo.InvariantCulture);
        }

        protected string LastLogin(object value)
        {
            if (value == null || value == DBNull.Value) return "never";
            return AppConfig.ToLocal(Convert.ToDateTime(value))
                .ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
