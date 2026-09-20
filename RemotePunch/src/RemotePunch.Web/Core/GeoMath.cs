using System;
using System.Globalization;

namespace RemotePunch.Web.Core
{
    /// <summary>Spherical-earth geometry used for every geofence decision.</summary>
    public static class GeoMath
    {
        /// <summary>Mean earth radius in metres (WGS-84 mean).</summary>
        public const double EarthRadiusMeters = 6371008.8;

        /// <summary>
        /// Great-circle distance in metres between two WGS-84 coordinates
        /// (haversine). Accurate to ~0.5% at any distance, which is far below
        /// the accuracy of a consumer GPS fix.
        /// </summary>
        public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);
            double rLat1 = ToRadians(lat1);
            double rLat2 = ToRadians(lat2);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(rLat1) * Math.Cos(rLat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(Math.Max(0.0, 1 - a)));
            return EarthRadiusMeters * c;
        }

        /// <summary>Initial bearing in degrees (0 = north) from point 1 to point 2.</summary>
        public static double BearingDegrees(double lat1, double lon1, double lat2, double lon2)
        {
            double rLat1 = ToRadians(lat1);
            double rLat2 = ToRadians(lat2);
            double dLon = ToRadians(lon2 - lon1);

            double y = Math.Sin(dLon) * Math.Cos(rLat2);
            double x = Math.Cos(rLat1) * Math.Sin(rLat2) - Math.Sin(rLat1) * Math.Cos(rLat2) * Math.Cos(dLon);
            double bearing = ToDegrees(Math.Atan2(y, x));
            return (bearing + 360.0) % 360.0;
        }

        public static string CompassPoint(double bearingDegrees)
        {
            string[] points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            int index = (int)Math.Round(((bearingDegrees % 360) / 45.0)) % 8;
            if (index < 0) index += 8;
            return points[index];
        }

        /// <summary>
        /// True when the coordinate is a real, usable fix. Null Island (0,0) is
        /// rejected because it is what broken clients report instead of an error.
        /// </summary>
        public static bool IsPlausibleCoordinate(double latitude, double longitude)
        {
            if (double.IsNaN(latitude) || double.IsNaN(longitude)) return false;
            if (double.IsInfinity(latitude) || double.IsInfinity(longitude)) return false;
            if (latitude < -90 || latitude > 90) return false;
            if (longitude < -180 || longitude > 180) return false;
            if (Math.Abs(latitude) < 0.000001 && Math.Abs(longitude) < 0.000001) return false;
            return true;
        }

        /// <summary>Implied ground speed in km/h between two fixes.</summary>
        public static double ImpliedSpeedKmh(double lat1, double lon1, DateTime timeUtc1,
                                             double lat2, double lon2, DateTime timeUtc2)
        {
            double seconds = Math.Abs((timeUtc2 - timeUtc1).TotalSeconds);
            if (seconds < 1) seconds = 1;
            double metres = DistanceMeters(lat1, lon1, lat2, lon2);
            return (metres / seconds) * 3.6;
        }

        /// <summary>Cache key for reverse geocoding: ~11 m buckets.</summary>
        public static string GeocodeCacheKey(double latitude, double longitude)
        {
            return latitude.ToString("F4", CultureInfo.InvariantCulture) + "," +
                   longitude.ToString("F4", CultureInfo.InvariantCulture);
        }

        public static string FormatDistance(double metres)
        {
            if (metres < 1000) return Math.Round(metres).ToString("F0", CultureInfo.InvariantCulture) + " m";
            return (metres / 1000.0).ToString("F2", CultureInfo.InvariantCulture) + " km";
        }

        private static double ToRadians(double degrees) { return degrees * Math.PI / 180.0; }
        private static double ToDegrees(double radians) { return radians * 180.0 / Math.PI; }
    }
}
