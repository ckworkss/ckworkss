using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using RemotePunch.Web.Core;
using RemotePunch.Web.Models;
using RemotePunch.Web.Services;

namespace RemotePunch.Web.Api
{
    /// <summary>Current punch state for the signed-in employee, as JSON.</summary>
    public class StatusHandler : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            AppUser user = AppUser.Current;
            if (user == null)
            {
                Json.Write(context.Response, new { ok = false, message = "Not signed in." }, 401);
                return;
            }

            PunchStatus status = new PunchService().GetStatus(user.EmployeeId);

            List<object> today = new List<object>();
            foreach (Punch punch in status.TodayPunches)
            {
                today.Add(new
                {
                    punchId = punch.PunchId,
                    type = punch.PunchType,
                    status = punch.Status,
                    timeLocal = punch.PunchTimeLocal.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    siteName = punch.SiteName,
                    distanceMeters = punch.DistanceMeters,
                    withinGeofence = punch.IsWithinGeofence
                });
            }

            List<object> sites = new List<object>();
            foreach (Site site in status.Sites)
            {
                sites.Add(new { id = site.SiteId, name = site.Name, lat = site.Latitude, lng = site.Longitude, radius = site.RadiusMeters });
            }

            Json.Write(context.Response, new
            {
                ok = true,
                nextAction = status.NextAction,
                isPunchedIn = status.IsPunchedIn,
                workedMinutesToday = status.WorkedMinutesToday,
                selfieRequired = status.SelfieRequired,
                allowRemotePunch = status.AllowRemotePunch,
                serverTimeLocal = AppConfig.ToLocal(DateTime.UtcNow).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                sites = sites,
                today = today
            }, 200);
        }
    }
}
