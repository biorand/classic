using IntelOrca.Biohazard.BioRand.Graphics;

namespace IntelOrca.Biohazard.BioRand.Cli;

internal sealed class BiorandCli
{
    private readonly TextWriter _out;
    private readonly TextWriter _err;

    public BiorandCli(TextWriter output, TextWriter error)
    {
        _out = output;
        _err = error;
    }

    public int Run(CliOptions options)
    {
        if (options.ShowHelp)
        {
            _out.Write(CliOptions.Usage);
            return 0;
        }

        if (options.ShowVersion)
        {
            _out.WriteLine($"biorand-cli {IntelOrca.Biohazard.Program.CurrentVersionInfo}");
            return 0;
        }

        try
        {
            RunCore(options);
            return 0;
        }
        catch (Exception ex) when (ex is CliUsageException or BioRandVersionException)
        {
            _err.WriteLine($"Error: {ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            _err.WriteLine($"Error: {ex.Message}");
            return 3;
        }
    }

    private void RunCore(CliOptions options)
    {
        var settingsPath = RandoAppSettings.GetSettingsPath();
        var settings = LoadSettings(settingsPath, _err, out var canSaveSettings);
        var (config, game) = SeedResolver.Resolve(options.Seed, settings);

        var mode = game == CliGame.Cv && !settings.GenerateCvPcMod
            ? OutputMode.CvPs2Iso
            : OutputMode.ModFolder;

        var input = ResolveInput(options.Input, settings, game);
        var output = PathUtil.Expand(options.Output!);
        ValidateOutput(input, output, mode);

        var plan = OutputPlan.Create(output, mode, options.Force);
        var scratchPath = game == CliGame.Cv ? CreateScratchDirectory() : null;
        try
        {
            plan.Prepare();

            var modeText = game == CliGame.Cv
                ? (mode == OutputMode.CvPs2Iso ? "CVX PS2 ISO" : "CVX PC mod")
                : "Classic REbirth mod folder";
            _err.WriteLine($"Info: Game: {CliGameInfo.Name(game)}");
            _err.WriteLine($"Info: Mode: {modeText}");
            _err.WriteLine($"Info: Input: {input}");
            _err.WriteLine($"Info: Output: {plan.OutputFolder}");
            if (mode == OutputMode.CvPs2Iso)
                _err.WriteLine($"Info: ISO: {plan.FinalPath}");
            _err.WriteLine($"Info: Seed: {config}");

            var installConfig = new ReInstallConfig
            {
                EnableCustomContent = !settings.DisableCustomContent,
                RandomizeTitleVoice = settings.RandomizeTitleVoice,
                MaxInventorySize = settings.MaxInventorySize,
                DoorSkip = settings.DoorSkip,
                BgmVolume = settings.GetBgmVolume((int)game),
                InstallPath = input,
                GenerateCvPcMod = mode == OutputMode.ModFolder,
                CvPcPortPath = "",
                ModPath = mode == OutputMode.CvPs2Iso ? scratchPath : plan.WorkingPath,
                TempPath = scratchPath,
                CvPs2OutputPath = mode == OutputMode.CvPs2Iso ? plan.WorkingPath : null
            };

            DataManager.ConfigureEmbeddedData(typeof(CliProgram).Assembly, "core-data/");

            var randomiser = CliGameInfo.CreateRandomiser(game, installConfig, new PortableBgCreator());
            if (game != CliGame.Cv && !randomiser.ValidateGamePath(input))
                throw new BioRandUserException($"'{input}' does not look like a {CliGameInfo.Name(game)} installation.");

            var progress = new CliProgress(_err, game == CliGame.Cv);
            progress.Start();
            randomiser.Generate(config, progress);

            if (mode == OutputMode.ModFolder)
                CaseMerge.Merge(plan.WorkingPath);

            if (game == CliGame.Cv)
                WriteCvMetadata(mode, plan, scratchPath!);

            plan.Commit();
            if (canSaveSettings)
                TrySaveSettings(settings, settingsPath, game, config, _err);
            progress.Complete();
        }
        finally
        {
            plan.Cleanup();
            if (scratchPath != null)
                TryDeleteDirectory(scratchPath);
        }
    }

    private static string ResolveInput(string? explicitInput, RandoAppSettings settings, CliGame game)
    {
        var raw = string.IsNullOrWhiteSpace(explicitInput)
            ? settings.GetGamePath((int)game)
            : explicitInput;

        if (string.IsNullOrWhiteSpace(raw))
            throw new CliUsageException($"No input path for {CliGameInfo.Name(game)}. Use -i or set it in settings.json.");

        var path = PathUtil.Expand(raw);
        if (game == CliGame.Cv)
        {
            if (File.Exists(path))
                return path;

            if (Directory.Exists(path))
            {
                var isoPath = Path.Combine(path, "recvx.iso");
                if (File.Exists(isoPath))
                    return isoPath;
            }

            throw new CliUsageException($"CVX ISO not found at '{path}'. Use -i to point at recvx.iso.");
        }

        if (File.Exists(path))
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                throw new CliUsageException($"Cannot determine the game folder from '{path}'.");
            path = directory;
        }

        if (!Directory.Exists(path))
            throw new CliUsageException($"Game folder not found at '{path}'.");
        return path;
    }

    private static void ValidateOutput(string input, string output, OutputMode mode)
    {
        if (PathUtil.IsRootOrHome(output))
            throw new CliUsageException($"Refusing to use '{output}' as an output folder.");

        if (mode == OutputMode.ModFolder && PathUtil.IsAncestor(output, input))
        {
            throw new CliUsageException(
                $"Output folder '{output}' contains the game data. Choose a dedicated mod folder.");
        }

        if (mode == OutputMode.CvPs2Iso)
        {
            var finalIso = Path.Combine(PathUtil.NormalizeDirectory(output), "recvx_biorand.iso");
            if (string.Equals(finalIso, input, PathUtil.Comparison))
                throw new CliUsageException("The output ISO would overwrite the input ISO. Choose a different output folder.");
        }
    }

    internal static void WriteCvMetadata(OutputMode mode, OutputPlan plan, string scratchPath)
    {
        var destination = mode == OutputMode.CvPs2Iso ? plan.OutputFolder : plan.WorkingPath;
        Directory.CreateDirectory(destination);

        foreach (var name in new[] { "log_pl0.txt", "graph_pl0.dgml" })
        {
            var sourcePath = Path.Combine(scratchPath, name);
            var destinationPath = Path.Combine(destination, name);
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, destinationPath, true);
            }
            else if (mode == OutputMode.CvPs2Iso && File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
        }
    }

    /// <summary>
    /// Loads settings.json. If it exists but cannot be read, default settings are
    /// returned and <paramref name="canSave"/> is false so the file is not overwritten.
    /// </summary>
    internal static RandoAppSettings LoadSettings(string path, TextWriter error, out bool canSave)
    {
        try
        {
            canSave = true;
            return RandoAppSettings.Load(path);
        }
        catch (Exception ex)
        {
            error.WriteLine($"Warning: could not read settings '{path}', it will not be updated: {ex.Message}");
            canSave = false;
            return new RandoAppSettings();
        }
    }

    /// <summary>
    /// Saves the generated seed and selected game. Other settings belong to the GUI
    /// and are written back unchanged.
    /// </summary>
    internal static bool TrySaveSettings(
        RandoAppSettings settings,
        string path,
        CliGame game,
        RandoConfig config,
        TextWriter error)
    {
        settings.SetSeed((int)game, config.ToString());
        settings.LastSelectedGame = CliGameInfo.GameIndex(game);
        try
        {
            settings.Save(path);
            return true;
        }
        catch (Exception ex)
        {
            error.WriteLine($"Warning: could not save settings '{path}': {ex.Message}");
            return false;
        }
    }

    private static string CreateScratchDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "biorand-cli");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
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
}
