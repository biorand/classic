namespace IntelOrca.Biohazard.BioRand.Cli;

/// <summary>
/// Windows merges file and directory names that differ only by case. Linux
/// keeps them separate, which produces duplicate roots such as Pl0/pl0.
/// This pass merges case variants deterministically so the generated mod has
/// the same tree on both platforms.
/// </summary>
internal static class CaseMerge
{
    public static void Merge(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return;
        MergeDirectory(root);
    }

    private static void MergeDirectory(string path)
    {
        var entries = Directory
            .EnumerateFileSystemEntries(path)
            .Select(x => new Entry(x, Directory.Exists(x)))
            .GroupBy(x => Path.GetFileName(x.Path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in entries)
        {
            var variants = group
                .OrderBy(x => Path.GetFileName(x.Path), StringComparer.Ordinal)
                .ToList();

            var target = variants[0];
            if (!target.IsDirectory)
            {
                var directory = variants.FirstOrDefault(x => x.IsDirectory);
                if (directory != null)
                    target = directory;
            }

            foreach (var variant in variants)
            {
                if (string.Equals(variant.Path, target.Path, StringComparison.Ordinal))
                    continue;
                MergeEntry(target, variant);
            }

            if (target.IsDirectory && Directory.Exists(target.Path))
                MergeDirectory(target.Path);
        }
    }

    private static void MergeEntry(Entry target, Entry source)
    {
        if (target.IsDirectory && source.IsDirectory)
        {
            foreach (var sourceChild in Directory.EnumerateFileSystemEntries(source.Path).ToList())
            {
                var name = Path.GetFileName(sourceChild);
                var sourceIsDirectory = Directory.Exists(sourceChild);
                var existing = Directory
                    .EnumerateFileSystemEntries(target.Path)
                    .FirstOrDefault(x => string.Equals(Path.GetFileName(x), name, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    var destination = Path.Combine(target.Path, name);
                    if (sourceIsDirectory)
                        Directory.Move(sourceChild, destination);
                    else
                        File.Move(sourceChild, destination);
                }
                else
                {
                    MergeEntry(
                        new Entry(existing, Directory.Exists(existing)),
                        new Entry(sourceChild, sourceIsDirectory));
                }
            }
            Directory.Delete(source.Path, true);
            return;
        }

        if (!target.IsDirectory && !source.IsDirectory)
        {
            // Keep the most recently written content, as Windows would.
            if (File.GetLastWriteTimeUtc(source.Path) > File.GetLastWriteTimeUtc(target.Path))
                File.Copy(source.Path, target.Path, true);
            File.Delete(source.Path);
            return;
        }

        // Unusual file/directory conflict. Keep the directory.
        if (target.IsDirectory)
        {
            if (source.IsDirectory)
                Directory.Delete(source.Path, true);
            else
                File.Delete(source.Path);
        }
        else
        {
            if (source.IsDirectory)
            {
                Directory.Delete(target.Path, true);
                Directory.Move(source.Path, target.Path);
            }
            else
            {
                File.Delete(source.Path);
            }
        }
    }

    private sealed record Entry(string Path, bool IsDirectory);
}
