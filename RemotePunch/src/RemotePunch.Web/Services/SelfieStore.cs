using System;
using System.Globalization;
using System.IO;
using System.Web;
using RemotePunch.Web.Core;

namespace RemotePunch.Web.Services
{
    /// <summary>
    /// Saves the optional punch selfie to disk. Files land under
    /// ~/Uploads/Selfies/yyyy/MM/dd and are only ever served back through
    /// Api/Selfie.ashx, which enforces who may look at them.
    /// </summary>
    public static class SelfieStore
    {
        /// <summary>JPEG magic bytes - anything else is refused.</summary>
        private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };

        /// <summary>
        /// Decodes a data URL / base64 JPEG and writes it out. Returns the path
        /// relative to the selfie root (stored in Punches.SelfiePath), or null.
        /// </summary>
        public static string Save(string base64, int employeeId, out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(base64)) return null;

            int comma = base64.IndexOf(',');
            if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            {
                base64 = base64.Substring(comma + 1);
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64.Trim());
            }
            catch (FormatException)
            {
                problem = "The captured photo could not be decoded.";
                return null;
            }

            if (bytes.Length == 0)
            {
                problem = "The captured photo was empty.";
                return null;
            }
            if (bytes.Length > AppConfig.SelfieMaxBytes)
            {
                problem = "The captured photo is larger than the " +
                          (AppConfig.SelfieMaxBytes / 1024) + " KB limit.";
                return null;
            }
            if (!LooksLikeJpeg(bytes))
            {
                problem = "Only JPEG photos are accepted.";
                return null;
            }

            DateTime now = DateTime.UtcNow;
            string relativeFolder = Path.Combine(
                now.Year.ToString("D4", CultureInfo.InvariantCulture),
                now.Month.ToString("D2", CultureInfo.InvariantCulture),
                now.Day.ToString("D2", CultureInfo.InvariantCulture));

            string root = HttpContext.Current.Server.MapPath(AppConfig.SelfieStoragePath);
            string folder = Path.Combine(root, relativeFolder);
            Directory.CreateDirectory(folder);

            string fileName = employeeId.ToString(CultureInfo.InvariantCulture) + "_" +
                              now.ToString("HHmmssfff", CultureInfo.InvariantCulture) + "_" +
                              Guid.NewGuid().ToString("N").Substring(0, 8) + ".jpg";

            File.WriteAllBytes(Path.Combine(folder, fileName), bytes);
            return relativeFolder.Replace('\\', '/') + "/" + fileName;
        }

        /// <summary>
        /// Maps a stored relative path back to a full path, refusing anything
        /// that tries to escape the selfie root.
        /// </summary>
        public static string ResolveFullPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            if (relativePath.IndexOf("..", StringComparison.Ordinal) >= 0) return null;
            if (relativePath.IndexOf(':') >= 0) return null;
            if (relativePath.StartsWith("/") || relativePath.StartsWith("\\")) return null;

            string root = HttpContext.Current.Server.MapPath(AppConfig.SelfieStoragePath);
            string fullRoot = Path.GetFullPath(root);
            string candidate = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

            if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) return null;
            return File.Exists(candidate) ? candidate : null;
        }

        private static bool LooksLikeJpeg(byte[] bytes)
        {
            if (bytes.Length < JpegMagic.Length) return false;
            for (int i = 0; i < JpegMagic.Length; i++)
            {
                if (bytes[i] != JpegMagic[i]) return false;
            }
            return true;
        }
    }
}
