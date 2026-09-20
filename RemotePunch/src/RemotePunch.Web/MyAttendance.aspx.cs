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
    public partial class MyAttendancePage : Page
    {
        protected TextBox txtFrom;
        protected TextBox txtTo;
        protected Button btnApply;
        protected HtmlAnchor lnkExport;
        protected Repeater rptDays;
        protected Repeater rptPunches;
        protected PlaceHolder phNoDays;
        protected PlaceHolder phNoPunches;

        private readonly PunchRepository _punches = new PunchRepository();

        protected void Page_Load(object sender, EventArgs e)
        {
            AppUser.Require();

            if (!IsPostBack)
            {
                DateTime today = PunchService.CurrentWorkDate();
                txtFrom.Text = today.AddDays(-14).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                txtTo.Text = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            Bind();
        }

        protected void btnApply_Click(object sender, EventArgs e)
        {
            Bind();
        }

        private void Bind()
        {
            AppUser user = AppUser.Require();
            DateTime from = PageHelpers.ParseDate(txtFrom.Text, PunchService.CurrentWorkDate().AddDays(-14));
            DateTime to = PageHelpers.ParseDate(txtTo.Text, PunchService.CurrentWorkDate());
            if (to < from) to = from;

            List<AttendanceDay> days = _punches.GetDailyAttendance(user.EmployeeId, from, to);
            rptDays.DataSource = days;
            rptDays.DataBind();
            rptDays.Visible = days.Count > 0;
            phNoDays.Visible = days.Count == 0;

            List<Punch> punches = _punches.Search(user.EmployeeId, from, to, null, null, false, 500);
            rptPunches.DataSource = punches;
            rptPunches.DataBind();
            rptPunches.Visible = punches.Count > 0;
            phNoPunches.Visible = punches.Count == 0;

            lnkExport.HRef = ResolveUrl("~/Api/Export.ashx?kind=attendance&from=" +
                from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "&to=" +
                to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        // ---- formatting used by both repeaters --------------------------

        protected string TimeText(object value) { return PageHelpers.TimeText(value); }
        protected string LateText(object value) { return PageHelpers.LateText(value); }
        protected string DistanceText(object value) { return PageHelpers.DistanceText(value); }
        protected string AccuracyText(object value) { return PageHelpers.AccuracyText(value); }
        protected string RowClass(object value) { return PageHelpers.RowClass(value); }
        protected string BadgeClass(object value) { return PageHelpers.BadgeClass(value); }

        protected string MapLink(object latitude, object longitude, object address)
        {
            return PageHelpers.MapLink(latitude, longitude, address);
        }

        protected string ReasonText(object dataItem)
        {
            return PageHelpers.ReasonText(dataItem as Punch);
        }
    }
}
