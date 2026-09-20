using System;
using System.Globalization;
using System.Web;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;
using RemotePunch.Web.Models;
using RemotePunch.Web.Services;

namespace RemotePunch.Web.Api
{
    /// <summary>
    /// Serves a stored punch photo. The Uploads folder itself is closed off in
    /// Web.config, so this is the only way in - and it checks that the caller
    /// either owns the punch or is allowed to review other people's.
    /// </summary>
    public class SelfieHandler : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            AppUser user = AppUser.Current;
            if (user == null)
            {
                context.Response.StatusCode = 401;
                return;
            }

            long punchId;
            if (!long.TryParse(context.Request.QueryString["punchId"], NumberStyles.Integer,
                               CultureInfo.InvariantCulture, out punchId))
            {
                context.Response.StatusCode = 400;
                return;
            }

            Punch punch = new PunchRepository().GetById(punchId);
            if (punch == null || !punch.HasSelfie)
            {
                context.Response.StatusCode = 404;
                return;
            }

            if (punch.EmployeeId != user.EmployeeId && !user.CanSeeOthers)
            {
                context.Response.StatusCode = 403;
                return;
            }

            string path = SelfieStore.ResolveFullPath(punch.SelfiePath);
            if (path == null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            context.Response.ContentType = "image/jpeg";
            context.Response.AppendHeader("Content-Disposition",
                "inline; filename=\"punch-" + punchId.ToString(CultureInfo.InvariantCulture) + ".jpg\"");
            context.Response.Cache.SetCacheability(HttpCacheability.Private);
            context.Response.TransmitFile(path);
        }
    }
}
