using System;
using System.Web.UI;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;

namespace RemotePunch.Web
{
    public partial class LogoutPage : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            AppUser user = AppUser.Current;
            if (user != null)
            {
                new AuditRepository().Write(user.EmployeeId, "SignOut", null, ClientInfo.FromRequest(Request));
            }

            AppUser.SignOut();
            Session.Abandon();
            Response.Redirect("~/Login.aspx", true);
        }
    }
}
