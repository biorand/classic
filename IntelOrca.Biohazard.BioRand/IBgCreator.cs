namespace IntelOrca.Biohazard.BioRand
{
    public interface IBgCreator
    {
        byte[] CreatePNG(RandoConfig config, byte[] pngBackground);
        uint[] CreateARGB(RandoConfig config, byte[] pngBackground);
        void DrawImage(TimFile timFile, byte[] srcImage, int x, int y);
        void DrawImage(TimFile timFile, byte[] srcImage, int x, int y, int clutIndex);
        uint[] LoadImage(string path);
        void SaveImage(string path, Tim2.Picture picture);
    }
}
