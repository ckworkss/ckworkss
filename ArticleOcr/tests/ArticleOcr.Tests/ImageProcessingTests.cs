using System;
using ArticleOcr.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArticleOcr.Tests
{
    [TestClass]
    public class ImageProcessingTests
    {
        /// <summary>White page with horizontal black "text lines", optionally rotated.</summary>
        private static GrayImage SyntheticLines(int w, int h, double angleDeg)
        {
            var img = new GrayImage(w, h);
            for (int i = 0; i < img.Data.Length; i++) img.Data[i] = 255;
            for (int y = h / 4; y < 3 * h / 4; y += 24)
                for (int yy = y; yy < y + 8; yy++)
                    for (int x = w / 5; x < 4 * w / 5; x++)
                        if ((x / 6) % 3 != 0) img[x, yy] = 0;     // broken strokes like glyphs
            return Math.Abs(angleDeg) < 1e-9 ? img : ImagePreprocessor.Rotate(img, angleDeg, 255);
        }

        [TestMethod]
        public void OtsuSeparatesTwoLevels()
        {
            var img = new GrayImage(10, 10);
            for (int i = 0; i < 100; i++) img.Data[i] = (byte)(i < 50 ? 40 : 200);
            var bin = ImagePreprocessor.Otsu(img);
            Assert.AreEqual(0, bin.Data[0]);
            Assert.AreEqual(255, bin.Data[99]);
        }

        [TestMethod]
        public void SauvolaHandlesGradientBackground()
        {
            // dark text on a background that darkens from left to right: a global threshold fails, Sauvola copes
            int w = 200, h = 60;
            var img = new GrayImage(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    img[x, y] = (byte)(230 - x * 0.6);
            for (int y = 25; y < 35; y++)
                for (int x = 0; x < w; x += 10)
                    for (int k = 0; k < 4; k++) img[x + k, y] = (byte)(img[x + k, y] - 90);
            var bin = ImagePreprocessor.Sauvola(img, 0.2, 21);
            Assert.AreEqual(0, bin[180, 30], "dark stroke on the dark side is still ink");
            Assert.AreEqual(255, bin[185, 30], "background on the dark side is still paper");
            Assert.AreEqual(0, bin[0, 30]);
            Assert.AreEqual(255, bin[5, 30]);
        }

        [TestMethod]
        public void ContrastStretchExpandsRange()
        {
            var img = new GrayImage(100, 1);
            for (int i = 0; i < 100; i++) img.Data[i] = (byte)(100 + i / 2);
            var s = ImagePreprocessor.ContrastStretch(img, 1, 99);
            Assert.IsTrue(s.Data[0] < 10);
            Assert.IsTrue(s.Data[99] > 245);
        }

        [TestMethod]
        public void RotateKeepsSizeAndContentForZeroAndQuadrants()
        {
            var img = SyntheticLines(120, 80, 0);
            var r90 = ImagePreprocessor.RotateQuadrant(img, 90);
            Assert.AreEqual(80, r90.Width);
            Assert.AreEqual(120, r90.Height);
            var back = ImagePreprocessor.RotateQuadrant(r90, 270);
            CollectionAssert.AreEqual(img.Data, back.Data);
            var r180 = ImagePreprocessor.RotateQuadrant(ImagePreprocessor.RotateQuadrant(img, 180), 180);
            CollectionAssert.AreEqual(img.Data, r180.Data);
        }

        [TestMethod]
        public void DeskewFindsRotationAngle()
        {
            var pre = new ImagePreprocessor { MaxSkew = 30 };
            foreach (var angle in new[] { 0.0, 7.0, -12.5, 21.0 })
            {
                var img = SyntheticLines(600, 400, angle);
                double est = pre.EstimateSkew(img);
                Assert.AreEqual(angle, est, 1.0, "angle " + angle + " estimated as " + est);
            }
        }

        [TestMethod]
        public void BuildVariantsDeskewsAndProducesThreeImages()
        {
            var pre = new ImagePreprocessor { MaxSkew = 30 };
            var img = SyntheticLines(600, 400, 10);
            var variants = pre.BuildVariants(img);
            Assert.AreEqual(3, variants.Count);
            Assert.AreEqual(10.0, variants[0].DeskewAngle, 1.0);
            // after deskew the rows should be sharp: the straight image's score beats the skewed one
            var straight = ImagePreprocessor.Otsu(variants[0].Image);
            Assert.IsTrue(Math.Abs(pre.EstimateSkew(straight)) < 1.0, "residual skew after correction");
        }

        [TestMethod]
        public void DownsampleReducesSize()
        {
            var img = SyntheticLines(1000, 500, 0);
            var small = ImagePreprocessor.Downsample(img, 250);
            Assert.IsTrue(small.Width <= 250 && small.Height <= 250);
            Assert.AreSame(img, ImagePreprocessor.Downsample(img, 2000), "no-op when already small");
        }
    }
}
