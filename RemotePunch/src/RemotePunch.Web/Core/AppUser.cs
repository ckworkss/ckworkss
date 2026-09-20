using System;
using System.Globalization;
using System.Security.Principal;
using System.Web;
using System.Web.Security;

namespace RemotePunch.Web.Core
{
    /// <summary>
    /// The signed-in employee, carried in the forms-authentication ticket so no
    /// server session is needed to know who is punching.
    /// </summary>
    public class AppUser
    {
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public bool MustChangePassword { get; set; }

        public bool IsAdmin { get { return string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase); } }
        public bool IsManager { get { return string.Equals(Role, "Manager", StringComparison.OrdinalIgnoreCase); } }
        public bool CanSeeOthers { get { return IsAdmin || IsManager; } }

        private const char Separator = '|';

        /// <summary>Issues the auth cookie. EmployeeId is the ticket name.</summary>
        public static void SignIn(HttpContextBase context, AppUser user, bool persistent)
        {
            string userData = string.Join(Separator.ToString(),
                user.EmployeeCode ?? string.Empty,
                (user.FullName ?? string.Empty).Replace(Separator, ' '),
                user.Role ?? "Employee",
                user.MustChangePassword ? "1" : "0");

            FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(
                2,
                user.EmployeeId.ToString(CultureInfo.InvariantCulture),
                DateTime.Now,
                DateTime.Now.AddMinutes(FormsAuthentication.Timeout.TotalMinutes),
                persistent,
                userData,
                FormsAuthentication.FormsCookiePath);

            string encrypted = FormsAuthentication.Encrypt(ticket);
            HttpCookie cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted);
            cookie.HttpOnly = true;
            cookie.Secure = FormsAuthentication.RequireSSL || context.Request.IsSecureConnection;
            cookie.Path = FormsAuthentication.FormsCookiePath;
            if (persistent) cookie.Expires = ticket.Expiration;
            context.Response.Cookies.Remove(FormsAuthentication.FormsCookieName);
            context.Response.Cookies.Add(cookie);
        }

        public static void SignOut()
        {
            FormsAuthentication.SignOut();
        }

        /// <summary>Rebuilds the user from a decrypted ticket. Returns null if malformed.</summary>
        public static AppUser FromTicket(FormsAuthenticationTicket ticket)
        {
            if (ticket == null) return null;

            int employeeId;
            if (!int.TryParse(ticket.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out employeeId)) return null;

            string[] parts = (ticket.UserData ?? string.Empty).Split(Separator);
            return new AppUser
            {
                EmployeeId = employeeId,
                EmployeeCode = parts.Length > 0 ? parts[0] : string.Empty,
                FullName = parts.Length > 1 ? parts[1] : string.Empty,
                Role = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : "Employee",
                MustChangePassword = parts.Length > 3 && parts[3] == "1"
            };
        }

        /// <summary>The user attached to the current request, or null when anonymous.</summary>
        public static AppUser Current
        {
            get
            {
                HttpContext context = HttpContext.Current;
                if (context == null) return null;

                AppUser cached = context.Items["RemotePunch.AppUser"] as AppUser;
                if (cached != null) return cached;

                IPrincipal principal = context.User;
                if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated) return null;

                FormsIdentity identity = principal.Identity as FormsIdentity;
                if (identity == null) return null;

                AppUser user = FromTicket(identity.Ticket);
                if (user != null) context.Items["RemotePunch.AppUser"] = user;
                return user;
            }
        }

        /// <summary>Current user or an exception - for code paths that already require auth.</summary>
        public static AppUser Require()
        {
            AppUser user = Current;
            if (user == null) throw new UnauthorizedAccessException("Not signed in.");
            return user;
        }
    }
}
