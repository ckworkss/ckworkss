using System;
using System.Collections.Generic;
using RemotePunch.Web.Core;
using RemotePunch.Web.Models;

namespace RemotePunch.Web.Services
{
    /// <summary>Outcome of matching one fix against an employee's sites.</summary>
    public class GeofenceMatch
    {
        public Site NearestSite { get; set; }
        public double? DistanceMeters { get; set; }
        /// <summary>The radius actually applied, after any accuracy allowance.</summary>
        public double EffectiveRadiusMeters { get; set; }
        public bool IsInside { get; set; }
        public bool HadCandidates { get; set; }
        /// <summary>Every candidate site with its distance, nearest first - shown on the punch screen.</summary>
        public List<SiteDistance> AllDistances { get; set; }

        public GeofenceMatch()
        {
            AllDistances = new List<SiteDistance>();
        }
    }

    public class SiteDistance
    {
        public Site Site { get; set; }
        public double DistanceMeters { get; set; }
        public bool IsInside { get; set; }
        public double BearingDegrees { get; set; }
    }

    /// <summary>
    /// Decides whether a coordinate falls inside one of the employee's sites.
    /// This runs on the server for every punch - the browser's own opinion of
    /// where it is relative to a geofence is only ever used to draw the UI.
    /// </summary>
    public static class GeofenceEvaluator
    {
        public static GeofenceMatch Evaluate(IEnumerable<Site> candidateSites, double latitude, double longitude,
                                             double? accuracyMeters)
        {
            GeofenceMatch match = new GeofenceMatch();
            if (candidateSites == null) return match;

            // A weak fix may legitimately land just outside a tight geofence, so
            // the accuracy radius can be added to the site radius - capped, so a
            // deliberately terrible fix cannot swallow the whole city.
            double bonus = 0;
            if (AppConfig.AddAccuracyToRadius && accuracyMeters.HasValue && accuracyMeters.Value > 0)
            {
                bonus = Math.Min(accuracyMeters.Value, AppConfig.MaxAccuracyBonusMeters);
            }

            foreach (Site site in candidateSites)
            {
                match.HadCandidates = true;

                double distance = GeoMath.DistanceMeters(latitude, longitude, site.Latitude, site.Longitude);
                double effectiveRadius = site.RadiusMeters + bonus;

                match.AllDistances.Add(new SiteDistance
                {
                    Site = site,
                    DistanceMeters = distance,
                    IsInside = distance <= effectiveRadius,
                    BearingDegrees = GeoMath.BearingDegrees(latitude, longitude, site.Latitude, site.Longitude)
                });
            }

            match.AllDistances.Sort(delegate (SiteDistance a, SiteDistance b)
            {
                return a.DistanceMeters.CompareTo(b.DistanceMeters);
            });

            // Credit the nearest site the employee is actually standing inside;
            // with no inside match, report the nearest one so the screen can say
            // how far away they are.
            SiteDistance chosen = null;
            foreach (SiteDistance candidate in match.AllDistances)
            {
                if (candidate.IsInside) { chosen = candidate; break; }
            }
            if (chosen == null && match.AllDistances.Count > 0) chosen = match.AllDistances[0];

            if (chosen != null)
            {
                match.NearestSite = chosen.Site;
                match.DistanceMeters = chosen.DistanceMeters;
                match.EffectiveRadiusMeters = chosen.Site.RadiusMeters + bonus;
                match.IsInside = chosen.IsInside;
            }

            return match;
        }
    }
}
