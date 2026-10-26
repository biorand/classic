using System;
using System.IO;
using StbImageSharp;
using StbImageWriteSharp;
using StbColorComponents = StbImageSharp.ColorComponents;

namespace IntelOrca.Biohazard.BioRand.Graphics
{
    /// <summary>
    /// A simple 32-bit straight alpha image (0xAARRGGBB per pixel).
    /// This is the portable replacement for GDI+ bitmaps used by the original
    /// <c>BiorandBgCreator</c>.
    /// </summary>
    internal sealed class ArgbImage
    {
        public int Width { get; }
        public int Height { get; }
        public uint[] Pixels { get; }

        public ArgbImage(int width, int height)
        {
            if (width < 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 0)
                throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            Pixels = new uint[width * height];
        }

        private ArgbImage(int width, int height, uint[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public static ArgbImage FromPixels(int width, int height, uint[] pixels)
        {
            if (pixels.Length < width * height)
                throw new ArgumentException("Not enough pixels for the specified dimensions", nameof(pixels));

            return new ArgbImage(width, height, pixels);
        }

        public static ArgbImage Decode(byte[] data)
        {
            var result = ImageResult.FromMemory(data, StbColorComponents.RedGreenBlueAlpha);
            return FromRgba(result.Data, result.Width, result.Height);
        }

        public static ArgbImage Decode(Stream stream)
        {
            var result = ImageResult.FromStream(stream, StbColorComponents.RedGreenBlueAlpha);
            return FromRgba(result.Data, result.Width, result.Height);
        }

        public static ArgbImage FromRgba(byte[] data, int width, int height)
        {
            if (data.Length < width * height * 4)
                throw new ArgumentException("Not enough RGBA data for the specified dimensions", nameof(data));

            var pixels = new uint[width * height];
            for (int i = 0, j = 0; i < pixels.Length; i++, j += 4)
            {
                var r = data[j + 0];
                var g = data[j + 1];
                var b = data[j + 2];
                var a = data[j + 3];
                pixels[i] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
            }
            return new ArgbImage(width, height, pixels);
        }

        public byte[] ToRgba()
        {
            var result = new byte[Width * Height * 4];
            for (int i = 0, j = 0; i < Pixels.Length; i++, j += 4)
            {
                var p = Pixels[i];
                result[j + 0] = (byte)(p >> 16);
                result[j + 1] = (byte)(p >> 8);
                result[j + 2] = (byte)(p >> 0);
                result[j + 3] = (byte)(p >> 24);
            }
            return result;
        }

        public void SavePng(Stream stream)
        {
            var writer = new ImageWriter();
            writer.WritePng(ToRgba(), Width, Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
        }

        public void SavePng(string path)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            SavePng(stream);
        }

        public uint GetPixel(int x, int y) => Pixels[(y * Width) + x];

        public void SetPixel(int x, int y, uint colour) => Pixels[(y * Width) + x] = colour;

        /// <summary>
        /// Composites <paramref name="source"/> over this image at the given position,
        /// clipping to the destination bounds. Straight alpha source-over blending is used.
        /// </summary>
        public void Blend(ArgbImage source, int x, int y)
        {
            var dstX0 = Math.Max(0, x);
            var dstY0 = Math.Max(0, y);
            var dstX1 = Math.Min(Width, x + source.Width);
            var dstY1 = Math.Min(Height, y + source.Height);
            if (dstX0 >= dstX1 || dstY0 >= dstY1)
                return;

            for (var dstY = dstY0; dstY < dstY1; dstY++)
            {
                var srcRow = (dstY - y) * source.Width;
                var dstRow = dstY * Width;
                for (var dstX = dstX0; dstX < dstX1; dstX++)
                {
                    var src = source.Pixels[srcRow + (dstX - x)];
                    var index = dstRow + dstX;
                    Pixels[index] = SourceOver(Pixels[index], src);
                }
            }
        }

        /// <summary>
        /// Composites a solid rectangle over this image, clipping to the destination bounds.
        /// </summary>
        public void FillRect(int x, int y, int width, int height, uint colour)
        {
            var dstX0 = Math.Max(0, x);
            var dstY0 = Math.Max(0, y);
            var dstX1 = Math.Min(Width, x + width);
            var dstY1 = Math.Min(Height, y + height);
            if (dstX0 >= dstX1 || dstY0 >= dstY1)
                return;

            for (var dstY = dstY0; dstY < dstY1; dstY++)
            {
                var dstRow = dstY * Width;
                for (var dstX = dstX0; dstX < dstX1; dstX++)
                {
                    var index = dstRow + dstX;
                    Pixels[index] = SourceOver(Pixels[index], colour);
                }
            }
        }

        /// <summary>
        /// Blends a single pixel over this image. Out of bounds coordinates are ignored.
        /// </summary>
        public void BlendPixel(int x, int y, uint colour)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return;

            var index = (y * Width) + x;
            Pixels[index] = SourceOver(Pixels[index], colour);
        }

        public static uint SourceOver(uint dst, uint src)
        {
            var sa = (int)(src >> 24) & 0xFF;
            if (sa == 0)
                return dst;
            if (sa == 255)
                return src;

            var da = (int)(dst >> 24) & 0xFF;
            if (da == 0)
                return src;

            var sr = (int)(src >> 16) & 0xFF;
            var sg = (int)(src >> 8) & 0xFF;
            var sb = (int)(src >> 0) & 0xFF;
            var dr = (int)(dst >> 16) & 0xFF;
            var dg = (int)(dst >> 8) & 0xFF;
            var db = (int)(dst >> 0) & 0xFF;

            // outA and the premultiplied colour components are kept in 0..255*255
            // so that everything stays in integer arithmetic.
            var outA = (sa * 255) + (da * (255 - sa));
            var outR = (((sr * sa * 255) + (dr * da * (255 - sa))) + (outA / 2)) / outA;
            var outG = (((sg * sa * 255) + (dg * da * (255 - sa))) + (outA / 2)) / outA;
            var outB = (((sb * sa * 255) + (db * da * (255 - sa))) + (outA / 2)) / outA;

            return ((uint)((outA + 127) / 255) << 24) |
                ((uint)outR << 16) |
                ((uint)outG << 8) |
                (uint)outB;
        }
    }
}
