using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace IntelOrca.Biohazard.BioRand
{
    internal sealed class ClassicRebirthModBuilder(string name)
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

        public string Name { get; set; } = name;
        public string? Description { get; set; }
        public ClassicRebirthModule? Module { get; set; }

        private void CreateManifest()
        {
            var sb = new StringBuilder();
            sb.Append("[MOD]\r\n");
            sb.Append($"Name = {Name}\r\n");
            if (Module is ClassicRebirthModule m)
            {
                sb.Append($"Module = {m.FileName}\r\n");
            }
            SetFile("manifest.txt", sb.ToString());
        }

        public void SetFile(string path, string data) => SetFile(path, Encoding.UTF8.GetBytes(data));

        public void SetFile(string path, ReadOnlyMemory<byte> data)
        {
            lock (_files)
            {
                _files[path] = data.ToArray();
            }
        }

        private void AddSupplementaryFiles()
        {
            CreateManifest();
            if (Module is ClassicRebirthModule m)
            {
                SetFile(m.FileName, m.Data);
            }
            if (Description is string d)
            {
                var processed = d
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Replace("\n", "\r\n");
                if (!processed.EndsWith("\r\n"))
                    processed += "\r\n";
                SetFile("description.txt", processed);
            }
        }

        public ClassicRebirthMod Build()
        {
            AddSupplementaryFiles();
            return new ClassicRebirthMod(_files.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase));
        }
    }
}
