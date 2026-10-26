using Net.Codecrete.QrCodeGenerator;

namespace IntelOrca.Biohazard.BioRand.Graphics
{
    /// <summary>
    /// Renders a QR code the same way QRCoder's
    /// <c>GetGraphic(pixelsPerModule, Color.White, Color.Transparent, true)</c> did:
    /// white modules on a transparent background, with a four module quiet zone.
    /// </summary>
    internal static class QrCodeRenderer
    {
        public const int QuietZoneModules = 4;

        public static int GetWidth(QrCode qrCode, int scale) =>
            (qrCode.Size + (QuietZoneModules * 2)) * scale;

        public static void Draw(ArgbImage target, QrCode qrCode, int x, int y, int scale, uint colour)
        {
            var size = qrCode.Size;
            for (var moduleY = 0; moduleY < size; moduleY++)
            {
                for (var moduleX = 0; moduleX < size; moduleX++)
                {
                    if (qrCode.GetModule(moduleX, moduleY))
                    {
                        target.FillRect(
                            x + ((moduleX + QuietZoneModules) * scale),
                            y + ((moduleY + QuietZoneModules) * scale),
                            scale,
                            scale,
                            colour);
                    }
                }
            }
        }
    }
}
