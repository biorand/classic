using IntelOrca.Biohazard.BioRand;
using IntelOrca.Biohazard.BioRand.RE1;
using IntelOrca.Biohazard.BioRand.RE2;
using IntelOrca.Biohazard.BioRand.RE3;
using IntelOrca.Biohazard.BioRand.RECV;

namespace IntelOrca.Biohazard.BioRand.Cli;

/// <summary>
/// A game, numbered as in <see cref="RandoConfig.Game"/>.
/// </summary>
public enum CliGame
{
    Re1 = 1,
    Re2 = 2,
    Re3 = 3,
    Cv = 4
}

public static class CliGameInfo
{
    public static string Name(CliGame game) => game switch
    {
        CliGame.Re1 => "RE1",
        CliGame.Re2 => "RE2",
        CliGame.Re3 => "RE3",
        CliGame.Cv => "CVX",
        _ => throw new ArgumentOutOfRangeException(nameof(game))
    };

    /// <summary>
    /// Converts to the zero based index used by <see cref="RandoAppSettings.LastSelectedGame"/>.
    /// </summary>
    public static int GameIndex(CliGame game) => (int)game - 1;

    public static bool TryFromGameIndex(int? index, out CliGame game)
    {
        if (index is >= 0 and <= 3)
        {
            game = (CliGame)(index.Value + 1);
            return true;
        }
        game = default;
        return false;
    }

    public static BaseRandomiser CreateRandomiser(CliGame game, ReInstallConfig installConfig, IBgCreator? bgCreator)
    {
        return game switch
        {
            CliGame.Re1 => new Re1Randomiser(installConfig, bgCreator),
            CliGame.Re2 => new Re2Randomiser(installConfig, bgCreator),
            CliGame.Re3 => new Re3Randomiser(installConfig, bgCreator),
            CliGame.Cv => new ReCvRandomiser(installConfig, bgCreator),
            _ => throw new ArgumentOutOfRangeException(nameof(game))
        };
    }
}
