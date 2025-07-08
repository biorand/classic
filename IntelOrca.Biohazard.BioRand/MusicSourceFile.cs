using System.Diagnostics;

namespace IntelOrca.Biohazard.BioRand
{
    [DebuggerDisplay("[{Game}, {Tag}, {Path}]")]
    public class MusicSourceFile(string path, string game, string tag)
    {
        public string Path => path;
        public string Game => game;
        public string Tag => tag;
    }
}
