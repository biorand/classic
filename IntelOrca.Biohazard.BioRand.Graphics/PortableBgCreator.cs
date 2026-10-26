using System;
using System.IO;
using Net.Codecrete.QrCodeGenerator;
using StbImageWriteSharp;

namespace IntelOrca.Biohazard.BioRand.Graphics
{
    /// <summary>
    /// Pure managed, NativeAOT friendly implementation of <see cref="IBgCreator"/>.
    /// It reproduces the GDI+ based <c>BiorandBgCreator</c> from the WPF application
    /// without using System.Drawing or any native imaging library.
    /// </summary>
    public sealed class PortableBgCreator : IBgCreator
    {
        private const uint White = 0xFFFFFFFF;

        public byte[] CreatePNG(RandoConfig config, byte[] pngBackground)
        {
            var titleBackground = CreateImage(config, pngBackground);
            using var memoryStream = new MemoryStream();
            titleBackground.SavePng(memoryStream);
            return memoryStream.ToArray();
        }

        public uint[] CreateARGB(RandoConfig config, byte[] pngBackground)
        {
            return CreateImage(config, pngBackground).Pixels;
        }

        private static ArgbImage CreateImage(RandoConfig config, byte[] pngBackground)
        {
            var sourceImage = ArgbImage.Decode(pngBackground);

            // HACK: The 1024x512 backgrounds (Resident Evil: Code Veronica) are
            // laid out differently, exactly as in the original implementation.
            var pictureRight = sourceImage.Width == 1024 ? 640 : sourceImage.Width;
            var xOffset = sourceImage.Width == 1024 ? 16 : 0;
            var yOffset = sourceImage.Width == 1024 ? 16 : 0;
            var size = sourceImage.Width == 1024 ? 2 : 1;

            var titleBackground = new ArgbImage(sourceImage.Width, sourceImage.Height);
            titleBackground.Blend(sourceImage, 0, 0);

            TtfTextRenderer.DrawText(
                titleBackground,
                Program.CurrentVersionInfo,
                xOffset,
                yOffset,
                14 * size,
                White);

            var seed = config.ToString();
            var qrCode = QrCode.EncodeText(seed, QrCode.Ecc.Medium);
            var qrWidth = QrCodeRenderer.GetWidth(qrCode, size);
            QrCodeRenderer.Draw(
                titleBackground,
                qrCode,
                pictureRight - xOffset - qrWidth,
                yOffset,
                size,
                White);

            return titleBackground;
        }

        public void DrawImage(TimFile timFile, byte[] srcImage, int x, int y)
        {
            var sourceImage = ArgbImage.Decode(srcImage);
            var destination = ArgbImage.FromPixels(timFile.Width, timFile.Height, timFile.GetPixels());
            destination.Blend(sourceImage, x, y);
            timFile.ImportPixels(destination.Pixels, 0);
        }

        public void DrawImage(TimFile timFile, byte[] srcImage, int x, int y, int clutIndex)
        {
            var sourceImage = ArgbImage.Decode(srcImage);
            var destinationWidth = timFile.Width;
            var destinationHeight = timFile.Height;

            // The GDI+ implementation relied on DrawImage clipping. ImportPixels has
            // no clipping, so clip the source rectangle manually before importing it.
            var sourceX = Math.Max(0, -x);
            var sourceY = Math.Max(0, -y);
            var width = Math.Min(sourceImage.Width - sourceX, destinationWidth - Math.Max(0, x));
            var height = Math.Min(sourceImage.Height - sourceY, destinationHeight - Math.Max(0, y));
            if (width <= 0 || height <= 0)
                return;

            var pixels = GetSubImage(sourceImage, sourceX, sourceY, width, height);
            timFile.ImportPixels(Math.Max(0, x), Math.Max(0, y), width, height, pixels, clutIndex);
        }

        public uint[] LoadImage(Stream data)
        {
            return ArgbImage.Decode(data).Pixels;
        }

        public void SaveImage(string path, Tim2.Picture picture)
        {
            if (picture.Depth != 8)
                return;

            var width = picture.Width;
            var height = picture.Height;

            var palette = new uint[256];
            for (var i = 0; i < palette.Length; i++)
            {
                var argb = Rgba2Argb(picture.GetColour(i));
                var a = Math.Min(((argb >> 24) & 0xFF) * 2, 255);
                palette[i] = ((uint)a << 24) | (argb & 0x00FFFFFFu);
            }

            var pixelData = picture.PixelData.Span;
            var rgba = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var colour = palette[pixelData[(y * width) + x]];
                    var offset = ((y * width) + x) * 4;
                    rgba[offset + 0] = (byte)(colour >> 16);
                    rgba[offset + 1] = (byte)(colour >> 8);
                    rgba[offset + 2] = (byte)(colour >> 0);
                    rgba[offset + 3] = (byte)(colour >> 24);
                }
            }

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            var writer = new ImageWriter();
            writer.WritePng(rgba, width, height, ColorComponents.RedGreenBlueAlpha, stream);
        }

        private static uint[] GetSubImage(ArgbImage image, int x, int y, int width, int height)
        {
            var result = new uint[width * height];
            var index = 0;
            for (var sy = y; sy < y + height; sy++)
            {
                for (var sx = x; sx < x + width; sx++)
                {
                    result[index++] = image.Pixels[(sy * image.Width) + sx];
                }
            }
            return result;
        }

        private static uint Rgba2Argb(int value)
        {
            var r = (uint)(value >> 0) & 0xFF;
            var g = (uint)(value >> 8) & 0xFF;
            var b = (uint)(value >> 16) & 0xFF;
            var a = (uint)(value >> 24) & 0xFF;
            return (a << 24) | (r << 16) | (g << 8) | b;
        }
    }
}
