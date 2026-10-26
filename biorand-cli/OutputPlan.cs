namespace IntelOrca.Biohazard.BioRand.Cli;

internal enum OutputMode
{
    ModFolder,
    CvPs2Iso
}

internal sealed class OutputPlan
{
    private readonly bool _force;

    public OutputMode Mode { get; }
    public string OutputFolder { get; }
    public string FinalPath { get; }
    public string WorkingPath { get; private set; } = "";

    private OutputPlan(OutputMode mode, string outputFolder, string finalPath, bool force)
    {
        Mode = mode;
        OutputFolder = outputFolder;
        FinalPath = finalPath;
        _force = force;
    }

    public static OutputPlan Create(string output, OutputMode mode, bool force)
    {
        var fullOutput = PathUtil.NormalizeDirectory(output);
        if (mode == OutputMode.CvPs2Iso)
        {
            return new OutputPlan(mode, fullOutput, Path.Combine(fullOutput, "recvx_biorand.iso"), force);
        }
        return new OutputPlan(mode, fullOutput, fullOutput, force);
    }

    public void Prepare()
    {
        if (Mode == OutputMode.CvPs2Iso)
        {
            PreparePs2();
        }
        else
        {
            PrepareModFolder();
        }
    }

    private void PrepareModFolder()
    {
        if (File.Exists(FinalPath))
            throw new CliUsageException($"Output path '{FinalPath}' is a file, not a folder.");

        var parent = Path.GetDirectoryName(FinalPath);
        if (string.IsNullOrEmpty(parent))
            throw new CliUsageException($"Output path '{FinalPath}' has no parent folder.");
        Directory.CreateDirectory(parent);

        if (Directory.Exists(FinalPath) && !PathUtil.DirectoryIsEmpty(FinalPath))
        {
            if (!_force)
                throw new CliUsageException($"Output folder '{FinalPath}' is not empty. Use -f or --force to replace it.");
            if (!LooksLikeBioRandOutput(FinalPath))
            {
                throw new CliUsageException(
                    $"Refusing to replace '{FinalPath}' because it does not look like a BioRand output. " +
                    "Choose another folder or remove it manually.");
            }
        }

        WorkingPath = FinalPath + ".tmp-" + Guid.NewGuid().ToString("N");
        if (Directory.Exists(WorkingPath))
            Directory.Delete(WorkingPath, true);
        Directory.CreateDirectory(WorkingPath);
    }

    private void PreparePs2()
    {
        if (File.Exists(OutputFolder))
            throw new CliUsageException($"Output path '{OutputFolder}' is a file, not a folder.");
        Directory.CreateDirectory(OutputFolder);

        if (Directory.Exists(FinalPath))
            throw new CliUsageException($"Output path '{FinalPath}' is a directory, not an ISO file.");

        if (File.Exists(FinalPath) && !_force)
            throw new CliUsageException($"Output file '{FinalPath}' already exists. Use -f or --force to replace it.");

        WorkingPath = FinalPath + ".tmp-" + Guid.NewGuid().ToString("N");
        if (File.Exists(WorkingPath))
            File.Delete(WorkingPath);
    }

    public void Commit()
    {
        var backupPath = FinalPath + ".bak-" + Guid.NewGuid().ToString("N");
        if (Mode == OutputMode.CvPs2Iso)
        {
            var hadExisting = File.Exists(FinalPath);
            if (hadExisting)
                File.Move(FinalPath, backupPath);

            try
            {
                File.Move(WorkingPath, FinalPath);
            }
            catch
            {
                if (hadExisting && File.Exists(backupPath))
                    File.Move(backupPath, FinalPath);
                throw;
            }

            if (hadExisting)
                TryDeleteFile(backupPath);
        }
        else
        {
            var hadExisting = Directory.Exists(FinalPath);
            if (hadExisting)
                Directory.Move(FinalPath, backupPath);

            try
            {
                Directory.Move(WorkingPath, FinalPath);
            }
            catch
            {
                if (hadExisting && Directory.Exists(backupPath))
                {
                    if (Directory.Exists(FinalPath))
                        Directory.Delete(FinalPath, true);
                    Directory.Move(backupPath, FinalPath);
                }
                throw;
            }

            if (hadExisting)
                TryDeleteDirectory(backupPath);
        }
    }

    private static bool LooksLikeBioRandOutput(string path)
    {
        try
        {
            var manifestPath = Path.Combine(path, "manifest.txt");
            if (File.Exists(manifestPath))
            {
                var manifest = File.ReadAllText(manifestPath);
                if (manifest.Contains("[MOD]", StringComparison.OrdinalIgnoreCase) &&
                    manifest.Contains("biorand.dll", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // CVX PC mods have no manifest. The room overrides and AFS
            // overrides are distinctive.
            return Directory.EnumerateFiles(path, "RM_*.RDX").Any() &&
                (Directory.Exists(Path.Combine(path, "SYSTEM.AFS")) ||
                 Directory.Exists(Path.Combine(path, "ADV.AFS")));
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
        }
    }

    public void Cleanup()
    {
        try
        {
            if (Mode == OutputMode.CvPs2Iso)
            {
                if (File.Exists(WorkingPath))
                    File.Delete(WorkingPath);
            }
            else
            {
                if (Directory.Exists(WorkingPath))
                    Directory.Delete(WorkingPath, true);
            }
        }
        catch
        {
        }
    }
}
