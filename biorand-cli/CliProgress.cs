using IntelOrca.Biohazard.BioRand;

namespace IntelOrca.Biohazard.BioRand.Cli;

internal sealed class CliProgress(TextWriter writer, bool isCvx) : IRandoProgress
{
    private readonly object _sync = new();
    private int _percent;
    private bool _started;
    private bool _completed;

    public void Start()
    {
        lock (_sync)
        {
            if (_started)
                return;
            _started = true;
            WriteProgress(0, "Starting");
        }
    }

    public void Complete()
    {
        lock (_sync)
        {
            if (_completed)
                return;
            if (!_started)
            {
                _started = true;
                WriteProgress(0, "Starting");
            }
            _completed = true;
            _percent = 100;
            WriteProgress(100, "Done");
        }
    }

    public IDisposable BeginTask(int? player, string message)
    {
        lock (_sync)
        {
            if (!_started)
            {
                _started = true;
                WriteProgress(0, "Starting");
            }

            if (message.StartsWith("Warning: ", StringComparison.Ordinal))
            {
                writer.WriteLine(message);
                writer.Flush();
                return NullScope.Instance;
            }

            if (IsFileNoise(message))
                return NullScope.Instance;

            var percent = GetPercent(message);
            _percent = Math.Max(_percent, percent);
            if (player is int p)
                WriteProgress(_percent, $"[P{p + 1}] {message}");
            else
                WriteProgress(_percent, message);
        }
        return NullScope.Instance;
    }

    private int GetPercent(string message)
    {
        if (Contains(message, "Clearing")) return 2;
        if (Contains(message, "Generating seed")) return 5;
        if (Contains(message, "Reading ISO file")) return 8;
        if (Contains(message, "Creating backgrounds")) return isCvx ? 10 : 97;
        if (Contains(message, "Changing player character")) return 40;
        if (Contains(message, "Randomizing doors")) return 20;
        if (Contains(message, "Randomizing items")) return 35;
        if (Contains(message, "Randomizing NPCs")) return 50;
        if (Contains(message, "Scanning voices")) return 45;
        if (Contains(message, "Randomizing enemies")) return 65;
        if (Contains(message, "Generating BGM")) return 90;
        if (Contains(message, "Randomizing BGM")) return 90;
        if (Contains(message, "Replacing enemies")) return 75;
        if (Contains(message, "Randomizing enemy skins")) return 80;
        if (Contains(message, "Randomizing portraits")) return 85;
        if (Contains(message, "Writing RDT files")) return 88;
        if (Contains(message, "Compressing room files")) return 92;
        if (Contains(message, "Creating ISO file")) return 96;
        if (Contains(message, "Writing PC port mod")) return 96;
        if (Contains(message, "Copying title card sounds")) return 98;
        if (Contains(message, "Copying biorand.dll")) return 99;
        if (Contains(message, "Writing manifest")) return 99;
        return Math.Min(95, _percent + 1);
    }

    private static bool Contains(string message, string value) =>
        message.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static bool IsFileNoise(string message) =>
        message.StartsWith("Copying '", StringComparison.Ordinal) ||
        message.StartsWith("Reading '", StringComparison.Ordinal) ||
        message.StartsWith("Writing '", StringComparison.Ordinal) ||
        message.StartsWith("Processing ", StringComparison.Ordinal) ||
        message.StartsWith("Converting ", StringComparison.Ordinal) ||
        message.StartsWith("Scanning '", StringComparison.Ordinal) ||
        message.StartsWith("Dumping ", StringComparison.Ordinal) ||
        message.Equals("Randomizing BGM", StringComparison.OrdinalIgnoreCase);

    private void WriteProgress(int percent, string message)
    {
        writer.WriteLine($"Progress: {percent}% {message}");
        writer.Flush();
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
