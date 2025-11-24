using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.BioRand
{
    internal static class Kernel32
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool DeleteFile(string lpFileName);
    }
}
