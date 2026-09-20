using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using RemotePunch.Web.Models;
using RemotePunch.Web.Services;

namespace RemotePunch.Web.Core
{
    /// <summary>
    /// Formatting shared by every list screen, so a punch looks the same on the
    /// punch page, the employee's history and the admin reports. Everything
    /// here returns HTML, so every value that came from a user is encoded.
    /// </summary>
    public static class PageHelpers
    {
        private const string Dash = "&mdash;";

        public static DateTime ParseDate(string value, DateTime fallback)
        {
            DateTime parsed;
            if (!string.IsNullOrWhiteSpace(value) &&
                DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed.Date;
            }
            return fallback.Date;
        }

        public static int? ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? (int?)parsed : null;
        }

        public static string Encode(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : HttpUtility.HtmlEncode(value);
        }

        public static string TextOrDash(object value)
        {
            string text = value == null || value == DBNull.Value ? null : Convert.ToString(value);
            return string.IsNullOrWhiteSpace(text) ? Dash : Encode(text);
        }

        public static string TimeText(object value)
        {
            if (value == null || value == DBNull.Value) return Dash;
            return Convert.ToDateTime(value).ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        public static string LateText(object value)
        {
            if (value == null || value == DBNull.Value) return Dash;
            int minutes = Convert.ToInt32(value);
            if (minutes <= 0) return "on time";
            return "<span class=\"badge badge-warn\">" +
                   minutes.ToString(CultureInfo.InvariantCulture) + " min</span>";
        }

        public static string DistanceText(object value)
        {
            if (value == null || value == DBNull.Value) return Dash;
            return GeoMath.FormatDistance(Convert.ToDouble(value));
        }

        public static string AccuracyText(object value)
        {
            if (value == null || value == DBNull.Value) return Dash;
            return "±" + GeoMath.FormatDistance(Convert.ToDouble(value));
        }

        public static string RowClass(object status)
        {
            string value = Convert.ToString(status);
            if (value == "Flagged") return "flagged";
            if (value == "Rejected") return "rejected";
            return string.Empty;
        }

        public static string BadgeClass(object status)
        {
            string value = Convert.ToString(status);
            if (value == "Accepted" || value == "Approved") return "badge-ok";
            if (value == "Flagged" || value == "Pending") return "badge-warn";
            if (value == "Rejected" || value == "Declined") return "badge-bad";
            return "badge-muted";
        }

        /// <summary>Address (or coordinates) linking out to the spot on a map.</summary>
        public static string MapLink(object latitude, object longitude, object address)
        {
            if (latitude == null || longitude == null) return Dash;

            double lat = Convert.ToDouble(latitude);
            double lng = Convert.ToDouble(longitude);
            string latText = lat.ToString("F6", CultureInfo.InvariantCulture);
            string lngText = lng.ToString("F6", CultureInfo.InvariantCulture);

            string label = address == null || address == DBNull.Value ? null : Convert.ToString(address);
            if (string.IsNullOrWhiteSpace(label)) label = latText + ", " + lngText;

            string url = "https://www.openstreetmap.org/?mlat=" + latText + "&mlon=" + lngText +
                         "#map=18/" + latText + "/" + lngText;

            return "<a href=\"" + Encode(url) + "\" target=\"_blank\" rel=\"noopener noreferrer\">" +
                   Encode(label) + "</a>";
        }

        /// <summary>Why a punch was rejected or flagged, else the employee's note.</summary>
        public static string ReasonText(Punch punch)
        {
            if (punch == null) return Dash;

            if (!string.IsNullOrEmpty(punch.RejectReason)) return Encode(punch.RejectReason);

            List<string> reasons = FlagCodes.DescribeAll(punch.FlagReasons);
            if (reasons.Count > 0) return Encode(string.Join(" ", reasons.ToArray()));

            return string.IsNullOrEmpty(punch.Note) ? Dash : Encode(punch.Note);
        }

        public static string MinutesText(int minutes)
        {
            return (minutes / 60).ToString("00", CultureInfo.InvariantCulture) + "h " +
                   (minutes % 60).ToString("00", CultureInfo.InvariantCulture) + "m";
        }
    }
}
