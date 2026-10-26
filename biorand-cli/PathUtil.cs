namespace IntelOrca.Biohazard.BioRand.Cli;

internal static class PathUtil
{
    public static StringComparison Comparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Expand(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        path = path.Trim();
        if (path.Length >= 2 && path[0] == '"' && path[^1] == '"')
            path = path[1..^1];

        if (path == "~")
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        else if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);

        path = Environment.ExpandEnvironmentVariables(path);
        return Path.GetFullPath(path);
    }

    public static string NormalizeDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (!string.IsNullOrEmpty(root) && full.Length > root.Length)
            full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return full;
    }

    public static bool IsAncestor(string parent, string child)
    {
        parent = NormalizeDirectory(parent);
        child = Path.GetFullPath(child);
        if (string.Equals(parent, child, Comparison))
            return true;

        var prefix = parent.EndsWith(Path.DirectorySeparatorChar)
            ? parent
            : parent + Path.DirectorySeparatorChar;
        return child.StartsWith(prefix, Comparison);
    }

    public static bool IsRootOrHome(string path)
    {
        var full = NormalizeDirectory(path);
        var root = Path.GetPathRoot(full);
        if (!string.IsNullOrEmpty(root) && string.Equals(full, NormalizeDirectory(root), Comparison))
            return true;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) && IsAncestor(full, home))
            return true;

        return false;
    }

    public static bool DirectoryIsEmpty(string path)
    {
        return !Directory.EnumerateFileSystemEntries(path).Any();
    }
}
