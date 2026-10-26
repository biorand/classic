using System.Globalization;
using System.Security.Cryptography;

namespace IntelOrca.Biohazard.BioRand.Cli;

internal static class SeedResolver
{
    /// <summary>
    /// Resolves the seed to generate. A seed string is used as is. A numeric seed, or no
    /// seed at all, uses the options from the last seed saved for the last selected game.
    /// </summary>
    public static (RandoConfig Config, CliGame Game) Resolve(string? seedArgument, RandoAppSettings settings)
    {
        int? numericSeed = null;
        if (!string.IsNullOrWhiteSpace(seedArgument))
        {
            var value = seedArgument.Trim();
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                var config = ParseSeedString(value);
                return (config, (CliGame)config.Game);
            }

            if (parsed < 0 || parsed > RandoConfig.MaxSeed)
                throw new CliUsageException($"Numeric seed must be between 0 and {RandoConfig.MaxSeed}.");
            numericSeed = parsed;
        }

        if (!CliGameInfo.TryFromGameIndex(settings.LastSelectedGame, out var game))
        {
            throw new CliUsageException(numericSeed == null
                ? "No seed specified and no last selected game in settings.json."
                : "A numeric seed needs a last selected game in settings.json.");
        }

        var result = GetSavedConfig(settings, game);
        result.Seed = numericSeed ?? RandomNumberGenerator.GetInt32(0, RandoConfig.MaxSeed + 1);
        return (result, game);
    }

    private static RandoConfig GetSavedConfig(RandoAppSettings settings, CliGame game)
    {
        var savedSeed = settings.GetSeed((int)game);
        if (string.IsNullOrWhiteSpace(savedSeed))
        {
            return new RandoConfig
            {
                Version = RandoConfig.LatestVersion,
                Game = (byte)game
            };
        }

        var config = ParseSeedString(savedSeed.Trim());
        if (config.Game != (byte)game)
        {
            throw new CliUsageException(
                $"The saved seed is for {CliGameInfo.Name((CliGame)config.Game)}, but the last selected game is {CliGameInfo.Name(game)}.");
        }
        return config;
    }

    private static RandoConfig ParseSeedString(string value)
    {
        RandoConfig config;
        try
        {
            config = RandoConfig.FromString(value);
        }
        catch (Exception ex)
        {
            throw new CliUsageException($"Invalid seed '{value}': {ex.Message}");
        }

        if (config.Version != RandoConfig.LatestVersion)
        {
            throw new CliUsageException(
                $"Seed version {config.Version} is not supported. This CLI supports version {RandoConfig.LatestVersion}.");
        }

        if (config.Game is < 1 or > 4)
            throw new CliUsageException($"Seed '{value}' does not specify a valid game.");

        return config;
    }
}
