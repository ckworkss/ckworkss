using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace ArticleOcr.Imaging
{
    /// <summary>8-bit grayscale raster (0 = black, 255 = white) with fast conversions to/from GDI+ bitmaps.</summary>
    public sealed class GrayImage
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Data { get; }

        public GrayImage(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("width/height");
            Width = width;
            Height = height;
            Data = new byte[width * height];
        }

        public GrayImage(int width, int height, byte[] data)
        {
            Width = width;
            Height = height;
            Data = data;
        }

        public byte this[int x, int y]
        {
            get { return Data[y * Width + x]; }
            set { Data[y * Width + x] = value; }
        }

        public GrayImage Clone()
        {
            return new GrayImage(Width, Height, (byte[])Data.Clone());
        }

        /// <summary>Convert any GDI+ bitmap to grayscale using Rec.601 luma.</summary>
        public static GrayImage FromBitmap(Bitmap bmp)
        {
            if (bmp == null) throw new ArgumentNullException(nameof(bmp));
            var img = new GrayImage(bmp.Width, bmp.Height);
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            using (var src = bmp.PixelFormat == PixelFormat.Format24bppRgb ? null : new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format24bppRgb))
            {
                Bitmap work = bmp;
                if (src != null)
                {
                    using (var g = Graphics.FromImage(src))
                    {
                        g.Clear(Color.White);
                        g.DrawImage(bmp, rect);
                    }
                    work = src;
                }
                var data = work.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    int stride = data.Stride;
                    var row = new byte[stride];
                    for (int y = 0; y < img.Height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), row, 0, stride);
                        int o = y * img.Width;
                        for (int x = 0, p = 0; x < img.Width; x++, p += 3)
                        {
                            // BGR order
                            img.Data[o + x] = (byte)((row[p + 2] * 299 + row[p + 1] * 587 + row[p] * 114) / 1000);
                        }
                    }
                }
                finally
                {
                    work.UnlockBits(data);
                }
            }
            return img;
        }

        /// <summary>Create an 8bpp indexed bitmap with a gray palette.</summary>
        public Bitmap ToBitmap()
        {
            var bmp = new Bitmap(Width, Height, PixelFormat.Format8bppIndexed);
            var pal = bmp.Palette;
            for (int i = 0; i < 256; i++) pal.Entries[i] = Color.FromArgb(i, i, i);
            bmp.Palette = pal;
            var rect = new Rectangle(0, 0, Width, Height);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            try
            {
                for (int y = 0; y < Height; y++)
                    Marshal.Copy(Data, y * Width, IntPtr.Add(data.Scan0, y * data.Stride), Width);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bmp;
        }

        public byte[] ToPng()
        {
            using (var bmp = ToBitmap())
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }
}
