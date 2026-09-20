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
    public partial class DashboardPage : Page
    {
        protected Literal litToday, litPresent, litPunches, litFlagged, litRejected, litPending, litSites;
        protected Repeater rptRecent;
        protected PlaceHolder phNone;

        protected void Page_Load(object sender, EventArgs e)
        {
            AppUser.Require();

            DateTime workDate = PunchService.CurrentWorkDate();
            litToday.Text = workDate.ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture);

            PunchRepository punches = new PunchRepository();
            Dictionary<string, int> counts = punches.GetDashboardCounts(workDate);

            litPresent.Text = Count(counts, "PresentCount");
            litPunches.Text = Count(counts, "PunchCount");
            litFlagged.Text = Count(counts, "FlaggedCount");
            litRejected.Text = Count(counts, "RejectedCount");
            litPending.Text = Count(counts, "PendingReviewCount");
            litSites.Text = Count(counts, "ActiveSiteCount");

            List<Punch> recent = punches.Search(null, workDate, workDate, null, null, false, 50);
            rptRecent.DataSource = recent;
            rptRecent.DataBind();
            rptRecent.Visible = recent.Count > 0;
            phNone.Visible = recent.Count == 0;
        }

        private static string Count(Dictionary<string, int> counts, string key)
        {
            int value;
            return counts.TryGetValue(key, out value) ? value.ToString(CultureInfo.InvariantCulture) : "0";
        }
    }
}
