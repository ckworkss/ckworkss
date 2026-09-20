using System;
using System.Collections.Generic;
using System.Web.UI;
using System.Web.UI.WebControls;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;
using RemotePunch.Web.Models;

namespace RemotePunch.Web.Admin
{
    public partial class SitesPage : Page
    {
        protected CheckBox chkIncludeInactive;
        protected Repeater rptSites;
        protected PlaceHolder phEmpty;

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
            List<Site> sites = new SiteRepository().GetAll(chkIncludeInactive.Checked);
            rptSites.DataSource = sites;
            rptSites.DataBind();
            rptSites.Visible = sites.Count > 0;
            phEmpty.Visible = sites.Count == 0;
        }
    }
}
