using System;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using RemotePunch.Web.Core;
using RemotePunch.Web.Models;
using RemotePunch.Web.Services;

namespace RemotePunch.Web
{
    public partial class LoginPage : Page
    {
        protected TextBox txtUser;
        protected TextBox txtPassword;
        protected CheckBox chkRemember;
        protected Button btnSignIn;
        protected PlaceHolder phError;
        protected Literal litError;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack && AppUser.Current != null)
            {
                Response.Redirect("~/Punch.aspx", true);
            }
        }

        protected void btnSignIn_Click(object sender, EventArgs e)
        {
            ClientInfo client = ClientInfo.FromRequest(Request);
            SignInResult result = new AuthService().SignIn(txtUser.Text, txtPassword.Text, client);

            if (!result.Succeeded)
            {
                ShowError(result.Message);
                return;
            }

            Employee employee = result.Employee;
            AppUser user = new AppUser
            {
                EmployeeId = employee.EmployeeId,
                EmployeeCode = employee.EmployeeCode,
                FullName = employee.FullName,
                Role = employee.Role,
                MustChangePassword = employee.MustChangePassword
            };

            // A fresh session id after sign-in closes the session-fixation door.
            Session.Abandon();
            AppUser.SignIn(new HttpContextWrapper(Context), user, chkRemember.Checked);

            if (employee.MustChangePassword)
            {
                Response.Redirect("~/ChangePassword.aspx?forced=1", true);
                return;
            }

            // Only ever follow a local return url.
            string returnUrl = Request.QueryString["ReturnUrl"];
            if (!string.IsNullOrEmpty(returnUrl) && IsLocalUrl(returnUrl))
            {
                Response.Redirect(returnUrl, true);
                return;
            }
            Response.Redirect(FormsAuthentication.DefaultUrl, true);
        }

        private static bool IsLocalUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (url.StartsWith("//", StringComparison.Ordinal)) return false;
            if (url.StartsWith("/\\", StringComparison.Ordinal)) return false;
            return url[0] == '/';
        }

        private void ShowError(string message)
        {
            phError.Visible = true;
            litError.Text = Server.HtmlEncode(message);
            txtPassword.Text = string.Empty;
        }
    }
}
