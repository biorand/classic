namespace IntelOrca.Biohazard.BioRand.Cli;

internal static class CliProgram
{
    public static int Main(string[] args)
    {
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (CliUsageException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine("Info: Use --help for usage.");
            return 2;
        }

        return new BiorandCli(Console.Out, Console.Error).Run(options);
    }
}
