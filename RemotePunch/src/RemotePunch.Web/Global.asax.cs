using System;
using System.Diagnostics;
using System.Net;
using System.Security.Principal;
using System.Web;
using System.Web.Security;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;

namespace RemotePunch.Web
{
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            // Reverse geocoding talks to HTTPS endpoints that refuse anything
            // older than TLS 1.2; .NET Framework does not pick this by default.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            ServicePointManager.Expect100Continue = false;
        }

        /// <summary>
        /// Turns the forms ticket into a principal carrying the employee's role,
        /// which is what the &lt;location path="Admin"&gt; rules in Web.config match on.
        /// </summary>
        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            HttpContext context = HttpContext.Current;
            if (context == null || context.User == null || context.User.Identity == null) return;
            if (!context.User.Identity.IsAuthenticated) return;

            FormsIdentity identity = context.User.Identity as FormsIdentity;
            if (identity == null) return;

            AppUser user = AppUser.FromTicket(identity.Ticket);
            if (user == null)
            {
                // Tampered or outdated cookie: drop it and start over.
                FormsAuthentication.SignOut();
                context.User = new GenericPrincipal(new GenericIdentity(string.Empty), new string[0]);
                return;
            }

            context.User = new GenericPrincipal(identity, new[] { user.Role });
            context.Items["RemotePunch.AppUser"] = user;

            EnforcePasswordChange(context, user);
        }

        /// <summary>
        /// An employee with a pending forced password change may only reach the
        /// change-password page, the sign-out link and static assets.
        /// </summary>
        private static void EnforcePasswordChange(HttpContext context, AppUser user)
        {
            if (!user.MustChangePassword) return;

            string path = context.Request.AppRelativeCurrentExecutionFilePath;
            if (string.IsNullOrEmpty(path)) return;

            if (path.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith("ChangePassword.aspx", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith("Login.aspx", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith("Logout.aspx", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith("Error.aspx", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("~/ChangePassword.aspx?forced=1", false);
                context.ApplicationInstance.CompleteRequest();
            }
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception exception = Server.GetLastError();
            if (exception == null) return;

            HttpException httpException = exception as HttpException;
            int statusCode = httpException != null ? httpException.GetHttpCode() : 500;

            // 404s are noise; everything else is worth a line in the trail.
            if (statusCode != 404)
            {
                Trace.TraceError("Unhandled error: " + exception);
                try
                {
                    AppUser user = AppUser.Current;
                    new AuditRepository().Write(
                        user == null ? (int?)null : user.EmployeeId,
                        "UnhandledError",
                        exception.GetType().Name + ": " + exception.Message,
                        ClientInfo.FromRequest(Request));
                }
                catch (Exception auditFailure)
                {
                    Trace.TraceError("Could not audit the error: " + auditFailure.Message);
                }
            }
        }
    }
}
