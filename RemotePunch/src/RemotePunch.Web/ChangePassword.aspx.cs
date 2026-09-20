using System;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;
using RemotePunch.Web.Models;
using RemotePunch.Web.Services;

namespace RemotePunch.Web
{
    public partial class ChangePasswordPage : Page
    {
        protected TextBox txtCurrent;
        protected TextBox txtNew;
        protected TextBox txtConfirm;
        protected Button btnSave;
        protected PlaceHolder phForced;
        protected PlaceHolder phMessage;
        protected Literal litMessage;

        public string MessageClass { get; private set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            AppUser.Require();
            MessageClass = "alert-bad";
            if (!IsPostBack) phForced.Visible = Request.QueryString["forced"] == "1";
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            AppUser user = AppUser.Require();

            if (txtNew.Text != txtConfirm.Text)
            {
                Show("The two new passwords do not match.", false);
                return;
            }

            string problem = new AuthService().ChangePassword(
                user.EmployeeId, txtCurrent.Text, txtNew.Text, ClientInfo.FromRequest(Request));

            if (problem != null)
            {
                Show(problem, false);
                return;
            }

            // The ticket carries MustChangePassword, so it has to be reissued
            // before the employee can move on.
            Employee employee = new EmployeeRepository().GetById(user.EmployeeId);
            AppUser refreshed = new AppUser
            {
                EmployeeId = employee.EmployeeId,
                EmployeeCode = employee.EmployeeCode,
                FullName = employee.FullName,
                Role = employee.Role,
                MustChangePassword = false
            };
            AppUser.SignIn(new HttpContextWrapper(Context), refreshed, false);

            Response.Redirect("~/Punch.aspx", true);
        }

        private void Show(string message, bool good)
        {
            phMessage.Visible = true;
            MessageClass = good ? "alert-ok" : "alert-bad";
            litMessage.Text = Server.HtmlEncode(message);
        }
    }
}
