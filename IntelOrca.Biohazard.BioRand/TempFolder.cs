using System;
using System.IO;

namespace IntelOrca.Biohazard.BioRand
{
    internal sealed class TempFolder : IDisposable
    {
        public string BasePath { get; }

        public TempFolder()
        {
            BasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(BasePath);
        }

        ~TempFolder() => Dispose();

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(BasePath))
                {
                    Directory.Delete(BasePath, true);
                }
            }
            catch
            {
            }
        }

        public string GetOrCreateDirectory(string path)
        {
            var newPath = Path.Combine(BasePath, path);
            Directory.CreateDirectory(newPath);
            return newPath;
        }
    }
}
