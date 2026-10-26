namespace IntelOrca.Biohazard.BioRand.Cli;

internal sealed class CliUsageException(string message) : Exception(message)
{
}

internal sealed class CliOptions
{
    public string? Seed { get; private set; }
    public string? Input { get; private set; }
    public string? Output { get; private set; }
    public bool Force { get; private set; }
    public bool ShowHelp { get; private set; }
    public bool ShowVersion { get; private set; }

    public const string Usage =
        "Usage: biorand-cli [-s seed] [-i input] [-o output] [-f]\n" +
        "\n" +
        "  -s, --seed <seed>      Seed string (R8...) or numeric seed. Optional.\n" +
        "                         A seed string selects the game. A numeric seed uses\n" +
        "                         the last selected game from settings.json.\n" +
        "  -i, --input <path>     Game data path. RE1/2/3: install directory. CVX: ISO.\n" +
        "                         Overrides the game path stored in settings.json.\n" +
        "  -o, --output <path>    Output folder. Required. For CVX PS2 the folder will\n" +
        "                         contain recvx_biorand.iso.\n" +
        "  -f, --force            Replace a previous BioRand output. Only the generated\n" +
        "                         mod folder or ISO is replaced.\n" +
        "      --help             Show this help.\n" +
        "      --version          Show the version.\n";

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    return options;
                case "--version":
                    options.ShowVersion = true;
                    return options;
                case "-f":
                case "--force":
                    options.Force = true;
                    break;
                case "-s":
                case "--seed":
                    options.Seed = ReadValue(args, ref i, arg);
                    break;
                case "-i":
                case "--input":
                    options.Input = ReadValue(args, ref i, arg);
                    break;
                case "-o":
                case "--output":
                    options.Output = ReadValue(args, ref i, arg);
                    break;
                default:
                    if (TryReadLongValue(args, ref i, "--seed", out var seed))
                        options.Seed = seed;
                    else if (TryReadLongValue(args, ref i, "--input", out var input))
                        options.Input = input;
                    else if (TryReadLongValue(args, ref i, "--output", out var output))
                        options.Output = output;
                    else if (arg.StartsWith("-", StringComparison.Ordinal))
                        throw new CliUsageException($"Unknown option '{arg}'.");
                    else
                        throw new CliUsageException($"Unexpected argument '{arg}'.");
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(options.Output))
            throw new CliUsageException("No output folder specified. Use -o or --output.");

        return options;
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
            throw new CliUsageException($"Option '{option}' needs a value.");
        return args[++index];
    }

    private static bool TryReadLongValue(string[] args, ref int index, string name, out string value)
    {
        var arg = args[index];
        if (arg.StartsWith(name + "=", StringComparison.Ordinal))
        {
            value = arg[(name.Length + 1)..];
            return true;
        }
        value = "";
        return false;
    }
}
