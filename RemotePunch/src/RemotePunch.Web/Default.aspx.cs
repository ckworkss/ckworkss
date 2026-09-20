using System;
using System.Web.UI;

namespace RemotePunch.Web
{
    /// <summary>Entry point: everyone starts at the punch screen.</summary>
    public partial class DefaultPage : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Redirect("~/Punch.aspx", true);
        }
    }
}
