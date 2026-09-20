using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;
using RemotePunch.Web.Core;
using RemotePunch.Web.Models;
using RemotePunch.Web.Services;

namespace RemotePunch.Web
{
    public partial class PunchPage : Page
    {
        protected Literal litToday;
        protected Literal litLastPunch;
        protected Literal litWorked;
        protected Repeater rptToday;
        protected PlaceHolder phNoPunches;
        protected PlaceHolder phSelfie;

        private readonly PunchService _punches = new PunchService();
        private PunchStatus _status;

        public bool IsPunchedIn { get { return _status != null && _status.IsPunchedIn; } }

        /// <summary>Everything punch.js needs, serialised into the page.</summary>
        public string ConfigJson { get; private set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            AppUser user = AppUser.Require();
            _status = _punches.GetStatus(user.EmployeeId);

            litToday.Text = PunchService.CurrentWorkDate().ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture);
            litWorked.Text = (_status.WorkedMinutesToday / 60).ToString("00", CultureInfo.InvariantCulture) + "h " +
                             (_status.WorkedMinutesToday % 60).ToString("00", CultureInfo.InvariantCulture) + "m";

            litLastPunch.Text = _status.LastPunch == null
                ? "No punches recorded yet."
                : "Last: " + _status.LastPunch.PunchType + " at " +
                  _status.LastPunch.PunchTimeLocal.ToString("dd MMM, HH:mm", CultureInfo.InvariantCulture) +
                  (_status.LastPunch.SiteName == null ? "" : " &middot; " + Server.HtmlEncode(_status.LastPunch.SiteName));

            phSelfie.Visible = _status.SelfieRequired;

            rptToday.DataSource = _status.TodayPunches;
            rptToday.DataBind();
            rptToday.Visible = _status.TodayPunches.Count > 0;
            phNoPunches.Visible = _status.TodayPunches.Count == 0;

            ConfigJson = BuildConfigJson(user);
        }

        private string BuildConfigJson(AppUser user)
        {
            List<object> sites = new List<object>();
            foreach (Site site in _status.Sites)
            {
                sites.Add(new
                {
                    id = site.SiteId,
                    name = site.Name,
                    lat = site.Latitude,
                    lng = site.Longitude,
                    radius = site.RadiusMeters
                });
            }

            return Json.Serialize(new
            {
                punchUrl = ResolveUrl("~/Api/Punch.ashx"),
                statusUrl = ResolveUrl("~/Api/Status.ashx"),
                employeeName = user.FullName,
                nextAction = _status.NextAction,
                sites = sites,
                selfieRequired = _status.SelfieRequired,
                selfieEnabled = AppConfig.SelfieEnabled,
                allowRemotePunch = _status.AllowRemotePunch,
                maxAccuracyMeters = AppConfig.MaxAccuracyMeters,
                warnAccuracyMeters = AppConfig.WarnAccuracyMeters,
                maxPositionAgeSeconds = AppConfig.MaxPositionAgeSeconds,
                // How long to keep sampling before settling for the best fix so far.
                settleMs = 12000,
                // A fix this good ends sampling immediately.
                goodEnoughMeters = Math.Min(25, AppConfig.WarnAccuracyMeters)
            });
        }

        // ---- repeater formatting (shared with the other list screens) ----

        protected string RowClass(object status) { return PageHelpers.RowClass(status); }
        protected string BadgeClass(object status) { return PageHelpers.BadgeClass(status); }
        protected string DistanceText(object metres) { return PageHelpers.DistanceText(metres); }
        protected string ReasonText(object dataItem) { return PageHelpers.ReasonText(dataItem as Punch); }
    }
}
