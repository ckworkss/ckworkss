using System;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;
using RemotePunch.Web.Core;

namespace RemotePunch.Web
{
    public partial class SiteMaster : MasterPage
    {
        protected PlaceHolder phSignedIn;
        protected PlaceHolder phReportsNav;
        protected PlaceHolder phAdminNav;
        protected PlaceHolder phUserBox;
        protected Literal litUserName;
        protected Literal litServerTime;
        protected Literal litTimeZone;

        protected void Page_Load(object sender, EventArgs e)
        {
            AppUser user = AppUser.Current;

            phSignedIn.Visible = user != null;
            phUserBox.Visible = user != null;
            phReportsNav.Visible = user != null && user.CanSeeOthers;
            phAdminNav.Visible = user != null && user.IsAdmin;

            if (user != null)
            {
                litUserName.Text = Server.HtmlEncode(user.FullName) +
                                   " <span class=\"role\">" + Server.HtmlEncode(user.Role) + "</span>";
            }

            litTimeZone.Text = Server.HtmlEncode(AppConfig.TimeZone.StandardName);
            litServerTime.Text = AppConfig.ToLocal(DateTime.UtcNow)
                .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
