using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using QRCoder;

namespace IntelOrca.Biohazard.BioRand
{
    internal class BiorandBgCreator : IBgCreator
    {
        public byte[] CreatePNG(RandoConfig config, byte[] pngBackground)
        {
            var ms = new MemoryStream();
            using (var titleBg = CreateImage(config, pngBackground))
            {
                titleBg.Save(ms, ImageFormat.Png);
            }
            return ms.ToArray();
        }

        public unsafe uint[] CreateARGB(RandoConfig config, byte[] pngBackground)
        {
            using (var titleBg = CreateImage(config, pngBackground))
            {
                return titleBg.ToArgb();
            }
        }

        private static void CreateBitmap(RandoConfig config, string modPath, string filename, byte[] src)
        {
            var destPath = Path.Combine(modPath, filename);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
            using (var titleBg = CreateImage(config, src))
            {
                titleBg.Save(destPath);
            }
        }

        private static Bitmap CreateImage(RandoConfig config, byte[] src)
        {
            using (var srcImage = new Bitmap(new MemoryStream(src)))
            {
                // HACK
                var pictureRight = srcImage.Width == 1024 ? 640 : srcImage.Width;
                var xOffset = srcImage.Width == 1024 ? 16 : 0;
                var yOffset = srcImage.Width == 1024 ? 16 : 0;
                var size = srcImage.Width == 1024 ? 2 : 1;

                var titleBg = new Bitmap(srcImage.Width, srcImage.Height);
                using (var g = Graphics.FromImage(titleBg))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                    g.DrawImage(srcImage, 0, 0, titleBg.Width, titleBg.Height);

                    var font = new Font("Courier New", 14 * size, GraphicsUnit.Pixel);

                    var versionInfo = Program.CurrentVersionInfo;
                    var versionSize = g.MeasureString(versionInfo, font);
                    g.DrawString(versionInfo, font, Brushes.White, xOffset, yOffset);

                    var seed = config.ToString();
                    // var seedSize = g.MeasureString(seed, font);
                    // g.DrawString(seed, font, Brushes.White, titleBg.Width - seedSize.Width + 1, 0);
                    var qr = GetQRImage(seed, size);
                    g.DrawImage(qr, pictureRight - xOffset - qr.Width, yOffset);
                }
                return titleBg;
            }
        }

        public void DrawImage(TimFile timFile, byte[] srcImage, int x, int y)
        {
            using (var srcBitmap = new Bitmap(new MemoryStream(srcImage)))
            {
                using (var bitmap = timFile.ToBitmap())
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.DrawImage(srcBitmap, x, y, srcBitmap.Width, srcBitmap.Height);
                    }
                    timFile.ImportBitmap(bitmap);
                }
            }
        }

        public void DrawImage(TimFile timFile, byte[] srcImage, int x, int y, int clutIndex)
        {
            using (var srcBitmap = new Bitmap(new MemoryStream(srcImage)))
            {
                timFile.ImportBitmap(srcBitmap, x, y, clutIndex);
            }
        }

        private static Bitmap GetQRImage(string text, int size)
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
            var qrCode = new QRCode(qrCodeData);
            var qrCodeImage = qrCode.GetGraphic(size, Color.White, Color.Transparent, true);
            return qrCodeImage;
        }

        public uint[] LoadImage(string path)
        {
            using (var bitmap = (Bitmap)Image.FromFile(path))
            {
                return bitmap.ToArgb();
            }
        }

        public unsafe void SaveImage(string path, Tim2.Picture picture)
        {
            if (picture.Depth != 8)
                return;

            var pixelData = picture.PixelData.Span;
            fixed (byte* p = pixelData)
            {
                var pixelFormat = picture.Depth == 8 ? PixelFormat.Format8bppIndexed : PixelFormat.Format4bppIndexed;
                var numColours = picture.Depth == 8 ? 256 : 16;
                using (var bitmap = new Bitmap(picture.Width, picture.Height, picture.Width, pixelFormat, (IntPtr)p))
                {
                    var palette = bitmap.Palette;
                    for (var i = 0; i < numColours; i++)
                    {
                        var argb = Rgba2Argb(picture.GetColour(i));
                        var c = Color.FromArgb(argb);
                        c = Color.FromArgb(Math.Min(c.A * 2, 255), c);
                        palette.Entries[i] = c;
                    }
                    bitmap.Palette = palette;
                    bitmap.Save(path);
                }
            }
        }

        private static int Rgba2Argb(int value)
        {
            int r = (value >> 0) & 0xFF;
            int g = (value >> 8) & 0xFF;
            int b = (value >> 16) & 0xFF;
            int a = (value >> 24) & 0xFF;
            return (a << 24) | (r << 16) | (g << 8) | b;
        }
    }
}
