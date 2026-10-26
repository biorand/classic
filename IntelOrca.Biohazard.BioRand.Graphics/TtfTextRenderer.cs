using System;
using System.Collections.Generic;
using System.IO;
using StbTrueTypeSharp;

namespace IntelOrca.Biohazard.BioRand.Graphics
{
    /// <summary>
    /// Minimal monochrome text renderer using the embedded Liberation Mono font.
    /// The original GDI+ implementation used Courier New at 14 pixels (or 28 pixels
    /// for the 1024x512 backgrounds) with SingleBitPerPixelGridFit text rendering.
    /// Liberation Mono is metrically compatible with Courier New.
    /// </summary>
    internal static unsafe class TtfTextRenderer
    {
        private const string FontResourceName = "IntelOrca.Biohazard.BioRand.Graphics.LiberationMono-Regular.ttf";

        private static readonly Lazy<byte[]> s_fontData = new Lazy<byte[]>(LoadFontData);
        private static readonly Dictionary<int, StbTrueType.stbtt_fontinfo> s_fonts = new Dictionary<int, StbTrueType.stbtt_fontinfo>();
        private static readonly object s_fontLock = new object();

        public static void DrawText(ArgbImage target, string text, int x, int y, int pixelSize, uint colour)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var font = GetFont(pixelSize);
            var scale = StbTrueType.stbtt_ScaleForMappingEmToPixels(font, pixelSize);

            int ascent;
            int descent;
            int lineGap;
            StbTrueType.stbtt_GetFontVMetrics(font, &ascent, &descent, &lineGap);

            // Match the top-left positioning of GDI+ DrawString: the supplied y is
            // the top of the line box, the baseline is one ascent below it.
            var baseline = y + (int)Math.Round(ascent * scale);
            var penX = x;

            foreach (var character in text)
            {
                var codepoint = character;
                int advanceWidth;
                int leftSideBearing;
                StbTrueType.stbtt_GetCodepointHMetrics(font, codepoint, &advanceWidth, &leftSideBearing);

                int width;
                int height;
                int xOffset;
                int yOffset;
                var bitmap = StbTrueType.stbtt_GetCodepointBitmap(
                    font, scale, scale, codepoint, &width, &height, &xOffset, &yOffset);
                try
                {
                    if (bitmap != null)
                    {
                        for (var glyphY = 0; glyphY < height; glyphY++)
                        {
                            for (var glyphX = 0; glyphX < width; glyphX++)
                            {
                                // SingleBitPerPixelGridFit means no anti-aliasing: a
                                // pixel is either set or not.
                                if (bitmap[(glyphY * width) + glyphX] >= 128)
                                {
                                    target.BlendPixel(penX + xOffset + glyphX, baseline + yOffset + glyphY, colour);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    if (bitmap != null)
                        StbTrueType.stbtt_FreeBitmap(bitmap, null);
                }

                penX += (int)Math.Round(advanceWidth * scale);
            }
        }

        private static StbTrueType.stbtt_fontinfo GetFont(int pixelSize)
        {
            lock (s_fontLock)
            {
                if (!s_fonts.TryGetValue(pixelSize, out var font))
                {
                    font = StbTrueType.CreateFont(s_fontData.Value, 0);
                    if (font == null)
                        throw new InvalidOperationException("Failed to load the embedded Liberation Mono font");

                    s_fonts.Add(pixelSize, font);
                }
                return font;
            }
        }

        private static byte[] LoadFontData()
        {
            var assembly = typeof(TtfTextRenderer).Assembly;
            using var stream = assembly.GetManifestResourceStream(FontResourceName);
            if (stream == null)
                throw new InvalidOperationException($"Missing embedded font resource: {FontResourceName}");

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
