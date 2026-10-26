using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace IntelOrca.Biohazard.BioRand;

public partial class DataManager
{
    private static Assembly? _embeddedAssembly;
    private static string _embeddedPrefix = "";
    private bool _embeddedAreaAdded;

    /// <summary>
    /// Adds an embedded core data area to every DataManager created afterwards.
    /// The area is added last, so data packs on disk override the embedded data.
    /// </summary>
    public static void ConfigureEmbeddedData(Assembly assembly, string prefix)
    {
        _embeddedAssembly = assembly;
        _embeddedPrefix = string.IsNullOrEmpty(prefix)
            ? ""
            : prefix.Replace('\\', '/').TrimEnd('/') + "/";
    }

    private void EnsureEmbeddedAreaLocked()
    {
        if (_embeddedAreaAdded)
            return;

        _embeddedAreaAdded = true;
        if (_embeddedAssembly != null)
            _areas.Add(new EmbeddedArea(_embeddedAssembly, _embeddedPrefix));
    }

    private sealed class EmbeddedArea(Assembly assembly, string prefix) : IArea
    {
        private readonly Dictionary<string, string> _paths = BuildPaths(assembly, prefix);

        private static Dictionary<string, string> BuildPaths(Assembly assembly, string prefix)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                var logicalPath = resourceName[prefix.Length..].Replace('\\', '/').Trim('/');
                if (logicalPath.Length != 0)
                    result[logicalPath] = resourceName;
            }
            return result;
        }

        public void Dispose()
        {
        }

        public Stream? GetData(string path)
        {
            var normalized = Normalize(path);
            return _paths.TryGetValue(normalized, out var resourceName)
                ? assembly.GetManifestResourceStream(resourceName)
                : null;
        }

        public string[] GetDirectories(string path)
        {
            var normalized = Normalize(path);
            var directoryPrefix = normalized.Length == 0 ? "" : normalized + "/";
            return _paths.Keys
                .Where(x => x.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(x => x[directoryPrefix.Length..])
                .Where(x => x.Contains('/'))
                .Select(x => x[..x.IndexOf('/')])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public string[] GetFiles(string path)
        {
            var normalized = Normalize(path);
            var directoryPrefix = normalized.Length == 0 ? "" : normalized + "/";
            return _paths.Keys
                .Where(x => x.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(x => x[directoryPrefix.Length..])
                .Where(x => x.Length != 0 && !x.Contains('/'))
                .ToArray();
        }

        private static string Normalize(string path)
        {
            return (path ?? "").Replace('\\', '/').Trim('/');
        }
    }
}
