using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ArticleOcr.Imaging
{
    /// <summary>A preprocessed image ready for OCR.</summary>
    public sealed class ImageVariant
    {
        public string Name { get; set; }
        public GrayImage Image { get; set; }
        public double DeskewAngle { get; set; }
        public int Rotation { get; set; }
    }

    /// <summary>
    /// Cleans up phone photos of roll labels before OCR:
    /// EXIF orientation, resizing, contrast stretch, Otsu and Sauvola binarisation, and deskew by
    /// projection-profile analysis. Produces several variants so the OCR engine can be run on each and the
    /// best pass kept.
    /// </summary>
    public sealed class ImagePreprocessor
    {
        /// <summary>Longest side is scaled down to this many pixels (speed) ...</summary>
        public int MaxDimension { get; set; } = 2200;
        /// <summary>... and up to this many pixels (Tesseract wants ~30px tall glyphs).</summary>
        public int MinDimension { get; set; } = 1400;
        /// <summary>Largest skew angle (degrees) searched by the deskewer.</summary>
        public double MaxSkew { get; set; } = 45;
        public double SauvolaK { get; set; } = 0.20;

        // ----------------------------------------------------------------------------------- loading

        public Bitmap Load(Stream stream)
        {
            var bmp = new Bitmap(stream);
            ApplyExifOrientation(bmp);
            return bmp;
        }

        /// <summary>Phones store the rotation in EXIF tag 0x0112; GDI+ does not apply it automatically.</summary>
        public static void ApplyExifOrientation(Bitmap bmp)
        {
            const int orientationId = 0x0112;
            if (Array.IndexOf(bmp.PropertyIdList, orientationId) < 0) return;
            var prop = bmp.GetPropertyItem(orientationId);
            if (prop == null || prop.Value == null || prop.Value.Length == 0) return;
            switch (prop.Value[0])
            {
                case 2: bmp.RotateFlip(RotateFlipType.RotateNoneFlipX); break;
                case 3: bmp.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                case 4: bmp.RotateFlip(RotateFlipType.Rotate180FlipX); break;
                case 5: bmp.RotateFlip(RotateFlipType.Rotate90FlipX); break;
                case 6: bmp.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                case 7: bmp.RotateFlip(RotateFlipType.Rotate270FlipX); break;
                case 8: bmp.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
            }
            try { bmp.RemovePropertyItem(orientationId); } catch (ArgumentException) { }
        }

        public Bitmap ResizeToWorkingSize(Bitmap bmp)
        {
            int longest = Math.Max(bmp.Width, bmp.Height);
            double scale = 1.0;
            if (longest > MaxDimension) scale = MaxDimension / (double)longest;
            else if (longest < MinDimension) scale = MinDimension / (double)longest;
            if (Math.Abs(scale - 1.0) < 0.01) return bmp;

            int w = Math.Max(1, (int)Math.Round(bmp.Width * scale));
            int h = Math.Max(1, (int)Math.Round(bmp.Height * scale));
            var dst = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.White);
                g.DrawImage(bmp, new Rectangle(0, 0, w, h));
            }
            return dst;
        }

        // ----------------------------------------------------------------------------------- variants

        /// <summary>Build the OCR variants for a loaded bitmap.</summary>
        public List<ImageVariant> BuildVariants(Bitmap source)
        {
            var sized = ResizeToWorkingSize(source);
            try
            {
                var gray = GrayImage.FromBitmap(sized);
                return BuildVariants(gray);
            }
            finally
            {
                if (!ReferenceEquals(sized, source)) sized.Dispose();
            }
        }

        public List<ImageVariant> BuildVariants(GrayImage gray)
        {
            var variants = new List<ImageVariant>();
            var stretched = ContrastStretch(gray, 1.0, 99.0);
            var sauvola = Sauvola(stretched, SauvolaK);
            double angle = EstimateSkew(sauvola);

            GrayImage baseGray = stretched;
            if (Math.Abs(angle) >= 0.4)
            {
                baseGray = Rotate(stretched, -angle, 255);
                sauvola = Sauvola(baseGray, SauvolaK);
            }

            variants.Add(new ImageVariant { Name = "gray", Image = baseGray, DeskewAngle = angle });
            variants.Add(new ImageVariant { Name = "sauvola", Image = sauvola, DeskewAngle = angle });
            variants.Add(new ImageVariant { Name = "otsu", Image = Otsu(baseGray), DeskewAngle = angle });
            return variants;
        }

        /// <summary>Rotate a variant by 90/180/270 degrees for labels photographed sideways.</summary>
        public ImageVariant RotateQuadrant(ImageVariant v, int degrees)
        {
            return new ImageVariant { Name = v.Name + "-rot" + degrees, Image = RotateQuadrant(v.Image, degrees), DeskewAngle = v.DeskewAngle, Rotation = degrees };
        }

        // ----------------------------------------------------------------------------------- filters

        /// <summary>Linear contrast stretch between two percentiles of the histogram.</summary>
        public static GrayImage ContrastStretch(GrayImage src, double lowPercentile, double highPercentile)
        {
            var hist = Histogram(src);
            int n = src.Data.Length;
            int lo = PercentileLevel(hist, n, lowPercentile);
            int hi = PercentileLevel(hist, n, highPercentile);
            if (hi - lo < 10) return src.Clone();
            var dst = new GrayImage(src.Width, src.Height);
            var lut = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                double v = (i - lo) * 255.0 / (hi - lo);
                lut[i] = (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
            }
            for (int i = 0; i < n; i++) dst.Data[i] = lut[src.Data[i]];
            return dst;
        }

        /// <summary>Global Otsu threshold: maximises between-class variance of the histogram.</summary>
        public static GrayImage Otsu(GrayImage src)
        {
            var hist = Histogram(src);
            int total = src.Data.Length;
            double sum = 0;
            for (int i = 0; i < 256; i++) sum += i * (double)hist[i];
            double sumB = 0; int wB = 0; double best = -1; int threshold = 127;
            for (int t = 0; t < 256; t++)
            {
                wB += hist[t];
                if (wB == 0) continue;
                int wF = total - wB;
                if (wF == 0) break;
                sumB += t * (double)hist[t];
                double mB = sumB / wB, mF = (sum - sumB) / wF;
                double between = (double)wB * wF * (mB - mF) * (mB - mF);
                if (between > best) { best = between; threshold = t; }
            }
            var dst = new GrayImage(src.Width, src.Height);
            for (int i = 0; i < total; i++) dst.Data[i] = src.Data[i] > threshold ? (byte)255 : (byte)0;
            return dst;
        }

        /// <summary>
        /// Sauvola adaptive threshold: T(x,y) = m (1 + k (s / R - 1)) with local mean m and standard deviation s
        /// computed through integral images. Handles the uneven lighting and shadows of warehouse photos.
        /// </summary>
        public static GrayImage Sauvola(GrayImage src, double k = 0.2, int window = 0, double r = 128)
        {
            int w = src.Width, h = src.Height;
            if (window <= 0) window = Math.Max(15, (Math.Min(w, h) / 30) | 1);
            int half = window / 2;

            var integral = new long[(w + 1) * (h + 1)];
            var integralSq = new double[(w + 1) * (h + 1)];
            int stride = w + 1;
            for (int y = 1; y <= h; y++)
            {
                long rowSum = 0; double rowSq = 0;
                for (int x = 1; x <= w; x++)
                {
                    int v = src.Data[(y - 1) * w + (x - 1)];
                    rowSum += v; rowSq += (double)v * v;
                    integral[y * stride + x] = integral[(y - 1) * stride + x] + rowSum;
                    integralSq[y * stride + x] = integralSq[(y - 1) * stride + x] + rowSq;
                }
            }

            var dst = new GrayImage(w, h);
            for (int y = 0; y < h; y++)
            {
                int y0 = Math.Max(0, y - half), y1 = Math.Min(h - 1, y + half);
                for (int x = 0; x < w; x++)
                {
                    int x0 = Math.Max(0, x - half), x1 = Math.Min(w - 1, x + half);
                    int area = (x1 - x0 + 1) * (y1 - y0 + 1);
                    long s = integral[(y1 + 1) * stride + (x1 + 1)] - integral[y0 * stride + (x1 + 1)] - integral[(y1 + 1) * stride + x0] + integral[y0 * stride + x0];
                    double sq = integralSq[(y1 + 1) * stride + (x1 + 1)] - integralSq[y0 * stride + (x1 + 1)] - integralSq[(y1 + 1) * stride + x0] + integralSq[y0 * stride + x0];
                    double mean = (double)s / area;
                    double var = sq / area - mean * mean;
                    double std = var > 0 ? Math.Sqrt(var) : 0;
                    double t = mean * (1 + k * (std / r - 1));
                    dst.Data[y * w + x] = src.Data[y * w + x] > t ? (byte)255 : (byte)0;
                }
            }
            return dst;
        }

        // ----------------------------------------------------------------------------------- deskew

        /// <summary>
        /// Estimate the text skew of a binarised image (black ink on white). The image is downsampled, rotated
        /// through candidate angles, and the angle whose horizontal projection profile is sharpest
        /// (largest sum of squared differences between neighbouring rows) wins. Coarse search then fine search.
        /// Returns degrees; positive means the text rises to the right.
        /// </summary>
        public double EstimateSkew(GrayImage binary)
        {
            var small = Downsample(binary, 480);
            var ink = new byte[small.Data.Length];
            for (int i = 0; i < ink.Length; i++) ink[i] = small.Data[i] < 128 ? (byte)1 : (byte)0;
            var mask = new GrayImage(small.Width, small.Height, ink);

            double bestAngle = 0, bestScore = double.NegativeInfinity;
            for (double a = -MaxSkew; a <= MaxSkew; a += 2.0)
            {
                double s = ProjectionScore(mask, a);
                if (s > bestScore) { bestScore = s; bestAngle = a; }
            }
            double center = bestAngle;
            for (double a = center - 2.0; a <= center + 2.0; a += 0.25)
            {
                double s = ProjectionScore(mask, a);
                if (s > bestScore) { bestScore = s; bestAngle = a; }
            }
            return bestAngle;
        }

        private static double ProjectionScore(GrayImage mask, double angleDeg)
        {
            int w = mask.Width, h = mask.Height;
            double rad = angleDeg * Math.PI / 180.0;
            double cos = Math.Cos(rad), sin = Math.Sin(rad);
            double cx = w / 2.0, cy = h / 2.0;
            // rows of the rotated image; use a generous canvas so nothing is clipped
            int rows = (int)Math.Ceiling(Math.Abs(w * sin) + Math.Abs(h * cos)) + 2;
            var profile = new int[rows];
            double offset = rows / 2.0;
            for (int y = 0; y < h; y++)
            {
                int o = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (mask.Data[o + x] == 0) continue;
                    double ry = -(x - cx) * sin + (y - cy) * cos + offset;
                    int iy = (int)ry;
                    if (iy >= 0 && iy < rows) profile[iy]++;
                }
            }
            double score = 0;
            for (int i = 1; i < rows; i++)
            {
                double d = profile[i] - profile[i - 1];
                score += d * d;
            }
            return score;
        }

        // ----------------------------------------------------------------------------------- geometry

        /// <summary>Rotate about the centre (bilinear), keeping the full rotated extent, filling with <paramref name="fill"/>.</summary>
        public static GrayImage Rotate(GrayImage src, double angleDeg, byte fill)
        {
            double rad = angleDeg * Math.PI / 180.0;
            double cos = Math.Cos(rad), sin = Math.Sin(rad);
            int w = src.Width, h = src.Height;
            int nw = (int)Math.Ceiling(Math.Abs(w * cos) + Math.Abs(h * sin));
            int nh = (int)Math.Ceiling(Math.Abs(w * sin) + Math.Abs(h * cos));
            var dst = new GrayImage(nw, nh);
            double cx = w / 2.0, cy = h / 2.0, ncx = nw / 2.0, ncy = nh / 2.0;
            for (int y = 0; y < nh; y++)
            {
                double dy = y - ncy;
                for (int x = 0; x < nw; x++)
                {
                    double dx = x - ncx;
                    // inverse mapping
                    double sx = dx * cos + dy * sin + cx;
                    double sy = -dx * sin + dy * cos + cy;
                    int x0 = (int)Math.Floor(sx), y0 = (int)Math.Floor(sy);
                    if (x0 < 0 || y0 < 0 || x0 >= w - 1 || y0 >= h - 1) { dst.Data[y * nw + x] = fill; continue; }
                    double fx = sx - x0, fy = sy - y0;
                    int o = y0 * w + x0;
                    double v = src.Data[o] * (1 - fx) * (1 - fy) + src.Data[o + 1] * fx * (1 - fy)
                             + src.Data[o + w] * (1 - fx) * fy + src.Data[o + w + 1] * fx * fy;
                    dst.Data[y * nw + x] = (byte)(v + 0.5);
                }
            }
            return dst;
        }

        public static GrayImage RotateQuadrant(GrayImage src, int degrees)
        {
            degrees = ((degrees % 360) + 360) % 360;
            int w = src.Width, h = src.Height;
            if (degrees == 0) return src.Clone();
            if (degrees == 180)
            {
                var d = new GrayImage(w, h);
                for (int i = 0, n = w * h; i < n; i++) d.Data[n - 1 - i] = src.Data[i];
                return d;
            }
            var dst = new GrayImage(h, w);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    byte v = src.Data[y * w + x];
                    if (degrees == 90) dst.Data[x * h + (h - 1 - y)] = v;      // clockwise
                    else dst.Data[(w - 1 - x) * h + y] = v;                       // 270 = counter-clockwise
                }
            return dst;
        }

        /// <summary>Box-filter downsample so the longest side is at most <paramref name="maxSide"/>.</summary>
        public static GrayImage Downsample(GrayImage src, int maxSide)
        {
            int longest = Math.Max(src.Width, src.Height);
            if (longest <= maxSide) return src;
            int f = (int)Math.Ceiling(longest / (double)maxSide);
            int nw = src.Width / f, nh = src.Height / f;
            var dst = new GrayImage(Math.Max(1, nw), Math.Max(1, nh));
            for (int y = 0; y < nh; y++)
                for (int x = 0; x < nw; x++)
                {
                    int sum = 0;
                    for (int yy = 0; yy < f; yy++)
                        for (int xx = 0; xx < f; xx++)
                            sum += src.Data[(y * f + yy) * src.Width + x * f + xx];
                    dst.Data[y * nw + x] = (byte)(sum / (f * f));
                }
            return dst;
        }

        // ----------------------------------------------------------------------------------- helpers

        private static int[] Histogram(GrayImage img)
        {
            var hist = new int[256];
            foreach (var b in img.Data) hist[b]++;
            return hist;
        }

        private static int PercentileLevel(int[] hist, int total, double percentile)
        {
            double target = total * percentile / 100.0;
            long acc = 0;
            for (int i = 0; i < 256; i++)
            {
                acc += hist[i];
                if (acc >= target) return i;
            }
            return 255;
        }
    }
}
