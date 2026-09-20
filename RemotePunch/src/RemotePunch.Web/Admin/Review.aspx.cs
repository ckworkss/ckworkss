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
    /// <summary>Approve or decline the punches the engine flagged.</summary>
    public partial class ReviewPage : Page
    {
        protected Repeater rptQueue;
        protected PlaceHolder phEmpty;
        protected PlaceHolder phMessage;
        protected Literal litMessage;

        private readonly PunchRepository _punches = new PunchRepository();

        protected void Page_Load(object sender, EventArgs e)
        {
            AppUser.Require();
            if (!IsPostBack) Bind();
        }

        private void Bind()
        {
            DateTime today = PunchService.CurrentWorkDate();
            List<Punch> queue = _punches.Search(null, today.AddDays(-90), today, null, null, true, 100);

            List<Punch> pending = new List<Punch>();
            foreach (Punch punch in queue)
            {
                if (string.Equals(punch.ReviewStatus, "Pending", StringComparison.Ordinal)) pending.Add(punch);
            }

            rptQueue.DataSource = pending;
            rptQueue.DataBind();
            rptQueue.Visible = pending.Count > 0;
            phEmpty.Visible = pending.Count == 0;
        }

        protected void rptQueue_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            AppUser user = AppUser.Require();

            long punchId;
            if (!long.TryParse(Convert.ToString(e.CommandArgument), NumberStyles.Integer,
                               CultureInfo.InvariantCulture, out punchId))
            {
                return;
            }

            bool approve = string.Equals(e.CommandName, "approve", StringComparison.OrdinalIgnoreCase);
            bool decline = string.Equals(e.CommandName, "decline", StringComparison.OrdinalIgnoreCase);
            if (!approve && !decline) return;

            TextBox noteBox = e.Item.FindControl("txtNote") as TextBox;
            string note = noteBox == null ? null : ClientInfo.Truncate(noteBox.Text.Trim(), 400);

            _punches.SetReview(punchId, approve ? "Approved" : "Declined", user.EmployeeId, note);
            new AuditRepository().Write(user.EmployeeId, approve ? "PunchApproved" : "PunchDeclined",
                "Punch #" + punchId.ToString(CultureInfo.InvariantCulture) +
                (string.IsNullOrEmpty(note) ? "" : " - " + note),
                ClientInfo.FromRequest(Request));

            phMessage.Visible = true;
            litMessage.Text = "Punch #" + punchId.ToString(CultureInfo.InvariantCulture) +
                              (approve ? " approved." : " declined.");
            Bind();
        }

        protected string ShortFingerprint(object value)
        {
            string text = Convert.ToString(value);
            if (string.IsNullOrEmpty(text)) return "&mdash;";
            return PageHelpers.Encode(text.Length > 16 ? text.Substring(0, 16) + "…" : text);
        }

        protected string FlagList(object dataItem)
        {
            Punch punch = dataItem as Punch;
            if (punch == null) return string.Empty;

            List<string> reasons = FlagCodes.DescribeAll(punch.FlagReasons);
            if (reasons.Count == 0) return "No specific findings were recorded.";

            System.Text.StringBuilder html = new System.Text.StringBuilder("<strong>Why it was flagged</strong><ul>");
            foreach (string reason in reasons) html.Append("<li>").Append(PageHelpers.Encode(reason)).Append("</li>");
            html.Append("</ul>");
            return html.ToString();
        }
    }
}
