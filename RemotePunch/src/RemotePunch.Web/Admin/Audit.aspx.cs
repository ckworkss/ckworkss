using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;

namespace RemotePunch.Web.Admin
{
    public partial class AuditPage : Page
    {
        protected DropDownList ddlEvent;
        protected TextBox txtRows;
        protected Button btnApply;
        protected Repeater rptAudit;
        protected PlaceHolder phEmpty;

        protected void Page_Load(object sender, EventArgs e)
        {
            AppUser.Require();
            if (!IsPostBack) Bind();
        }

        protected void btnApply_Click(object sender, EventArgs e)
        {
            Bind();
        }

        private void Bind()
        {
            int rows = PageHelpers.ParseInt(txtRows.Text).GetValueOrDefault(200);
            if (rows < 10) rows = 10;
            if (rows > 2000) rows = 2000;

            string eventType = string.IsNullOrEmpty(ddlEvent.SelectedValue) ? null : ddlEvent.SelectedValue;
            List<AuditEntry> entries = new AuditRepository().GetRecent(rows, null, eventType);

            rptAudit.DataSource = entries;
            rptAudit.DataBind();
            rptAudit.Visible = entries.Count > 0;
            phEmpty.Visible = entries.Count == 0;
        }

        protected string LocalTime(object value)
        {
            if (value == null || value == DBNull.Value) return "&mdash;";
            return AppConfig.ToLocal(Convert.ToDateTime(value))
                .ToString("dd MMM yyyy, HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
