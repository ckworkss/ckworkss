using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;
using RemotePunch.Web.Models;
using RemotePunch.Web.Services;

namespace RemotePunch.Web
{
    /// <summary>Read-only attendance reporting for managers and administrators.</summary>
    public partial class ReportsPage : Page
    {
        protected DropDownList ddlEmployee;
        protected DropDownList ddlStatus;
        protected TextBox txtFrom;
        protected TextBox txtTo;
        protected Button btnApply;
        protected HtmlAnchor lnkExportDays;
        protected HtmlAnchor lnkExportPunches;
        protected Repeater rptDays;
        protected Repeater rptPunches;
        protected PlaceHolder phNoDays;
        protected PlaceHolder phNoPunches;
        protected Literal litTotalDays;
        protected Literal litTotalHours;
        protected Literal litFlagged;

        private readonly PunchRepository _punches = new PunchRepository();

        protected void Page_Load(object sender, EventArgs e)
        {
            AppUser user = AppUser.Require();
            if (!user.CanSeeOthers)
            {
                Response.Redirect("~/MyAttendance.aspx", true);
                return;
            }

            if (!IsPostBack)
            {
                BindEmployees();
                DateTime today = PunchService.CurrentWorkDate();
                txtFrom.Text = today.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                txtTo.Text = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            Bind();
        }

        protected void btnApply_Click(object sender, EventArgs e)
        {
            Bind();
        }

        private void BindEmployees()
        {
            ddlEmployee.Items.Add(new ListItem("All employees", ""));
            foreach (Employee employee in new EmployeeRepository().GetAll(true))
            {
                ddlEmployee.Items.Add(new ListItem(
                    employee.FullName + " (" + employee.EmployeeCode + ")",
                    employee.EmployeeId.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private void Bind()
        {
            DateTime from = PageHelpers.ParseDate(txtFrom.Text, PunchService.CurrentWorkDate().AddDays(-7));
            DateTime to = PageHelpers.ParseDate(txtTo.Text, PunchService.CurrentWorkDate());
            if (to < from) to = from;

            int? employeeId = PageHelpers.ParseInt(ddlEmployee.SelectedValue);
            string status = string.IsNullOrEmpty(ddlStatus.SelectedValue) ? null : ddlStatus.SelectedValue;

            List<AttendanceDay> days = _punches.GetDailyAttendance(employeeId, from, to);
            rptDays.DataSource = days;
            rptDays.DataBind();
            rptDays.Visible = days.Count > 0;
            phNoDays.Visible = days.Count == 0;

            List<Punch> punches = _punches.Search(employeeId, from, to, status, null, false, 500);
            rptPunches.DataSource = punches;
            rptPunches.DataBind();
            rptPunches.Visible = punches.Count > 0;
            phNoPunches.Visible = punches.Count == 0;

            int totalMinutes = 0;
            foreach (AttendanceDay day in days) totalMinutes += day.WorkedMinutes;

            int flagged = 0;
            foreach (Punch punch in punches)
            {
                if (string.Equals(punch.Status, "Flagged", StringComparison.Ordinal)) flagged++;
            }

            litTotalDays.Text = days.Count.ToString(CultureInfo.InvariantCulture);
            litTotalHours.Text = (totalMinutes / 60.0).ToString("F1", CultureInfo.InvariantCulture);
            litFlagged.Text = flagged.ToString(CultureInfo.InvariantCulture);

            string query = "&from=" + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                           "&to=" + to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                           (employeeId.HasValue ? "&employeeId=" + employeeId.Value.ToString(CultureInfo.InvariantCulture) : "") +
                           (status == null ? "" : "&status=" + Server.UrlEncode(status));

            lnkExportDays.HRef = ResolveUrl("~/Api/Export.ashx?kind=attendance" + query);
            lnkExportPunches.HRef = ResolveUrl("~/Api/Export.ashx?kind=punches" + query);
        }
    }
}
