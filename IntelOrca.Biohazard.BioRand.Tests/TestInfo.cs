using System;
using System.IO;

namespace IntelOrca.Biohazard.BioRand.Tests
{
    internal static class TestInfo
    {
        public static string GetInstallPath(int game)
        {
            var fileName = $"re{game}";
            if (game == 4)
                fileName = "recvx";
            var places = new[]
            {
                $@"D:\games\{fileName}",
                $@"F:\games\{fileName}",
                $@"M:\games\{fileName}"
            };

            foreach (var place in places)
            {
                if (Directory.Exists(place))
                {
                    return place;
                }
            }
            throw new Exception("Unable to find RE.");
        }
    }
}
