using System;
using IntelOrca.Biohazard.BioRand.RE1;
using IntelOrca.Biohazard.BioRand.RE2;
using IntelOrca.Biohazard.BioRand.RE3;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Tests
{
    public class TestRE1Rando : TestBaseRandomizer
    {
        public override byte Game => 1;

        [Fact]
        public void SwapPlayers() => SwapPlayersInner();
    }

    public class TestRE2Rando : TestBaseRandomizer
    {
        public override byte Game => 2;

        [Fact]
        public void SwapPlayers() => SwapPlayersInner();

        [Fact]
        public void RandomizeEnemyPlacements() => RandomizeEnemyPlacementsInner();
    }

    public class TestRE3Rando : TestBaseRandomizer
    {
        public override byte Game => 3;

        [Fact]
        public void RandomizeEnemyPlacements() => RandomizeEnemyPlacementsInner();
    }

    public abstract class TestBaseRandomizer
    {
        public abstract byte Game { get; }

        [Fact]
        public void RandomizeDoors()
        {
            var config = GetBaseConfig();
            config.RandomDoors = true;
            Randomize(config);
        }

        protected void SwapPlayersInner()
        {
            var config = GetBaseConfig();
            config.SwapCharacters = true;
            Randomize(config);
        }

        protected void RandomizeEnemyPlacementsInner()
        {
            var config = GetBaseConfig();
            config.RandomEnemyPlacement = true;
            Randomize(config);
        }

        protected void RandomizeEnemySkins()
        {
            var config = GetBaseConfig();
            config.RandomEnemySkins = true;
            Randomize(config);
        }

        [Fact]
        public void RandomizeItems_Shuffle()
        {
            var config = GetBaseConfig();
            config.RandomDoors = false;
            config.AlternativeRoutes = false;
            config.ShuffleItems = true;
            Randomize(config);
        }

        [Fact]
        public void RandomizeItems_AlternativeRoutes()
        {
            var config = GetBaseConfig();
            config.RandomDoors = false;
            config.AlternativeRoutes = true;
            config.ShuffleItems = false;
            Randomize(config);
        }

        [Fact]
        public void RandomizeNPCs()
        {
            var config = GetBaseConfig();
            config.RandomDoors = false;
            config.RandomItems = false;
            config.RandomEnemies = false;
            config.RandomNPCs = false;
            config.ChangePlayer = false;
            config.RandomNPCs = true;
            config.RandomCutscenes = true;
            Randomize(config);
        }

        [Fact]
        public void RandomizeBGM()
        {
            var config = GetBaseConfig();
            config.RandomDoors = false;
            config.RandomItems = false;
            config.RandomEnemies = false;
            config.RandomNPCs = false;
            config.ChangePlayer = false;
            config.RandomBgm = true;
            config.EnabledBGMs = new bool[5];
            for (int i = 0; i < 5; i++)
                config.EnabledBGMs[i] = i % 2 == 0;
            Randomize(config);
        }

        protected void Randomize(RandoConfig config)
        {
            var rando = GetRandomizer();
            rando.Generate(config, new NullProgress());
        }

        private BaseRandomiser GetRandomizer()
        {
            var installConfig = GetInstallConfig();
            switch (Game)
            {
                case 1:
                    return new Re1Randomiser(installConfig, null);
                case 2:
                    return new Re2Randomiser(installConfig, null);
                case 3:
                    return new Re3Randomiser(installConfig, null);
                default:
                    throw new Exception("Invalid game");
            }
        }

        private RandoConfig GetBaseConfig()
        {
            var config = new RandoConfig();
            config.Player = 0;
            config.GameVariant = 0;
            config.Game = Game;
            config.Seed = 12345;

            config.RandomDoors = false;
            config.AreaCount = 2;
            config.AreaSize = 4;

            config.RandomItems = true;
            config.IncludeDocuments = true;
            config.AlternativeRoutes = false;
            config.Segmented = true;
            config.ShuffleItems = false;

            config.RandomEnemies = true;
            config.RandomEnemyPlacement = false;
            config.RandomEnemySkins = false;
            config.EnemyDifficulty = 2;
            config.EnemyRatios = new byte[5];
            for (int i = 0; i < 5; i++)
                config.EnemyRatios[i] = 7;

            config.RandomCutscenes = false;

            config.RandomNPCs = false;
            config.EnabledNPCs = new bool[10];
            for (int i = 0; i < 10; i++)
                config.EnabledNPCs[i] = i % 2 == 0;

            config.ChangePlayer = true;
            config.Player0 = 0;
            config.Player1 = 0;

            config.RandomBgm = false;
            config.EnabledBGMs = new bool[5];
            for (int i = 0; i < 5; i++)
                config.EnabledBGMs[i] = i % 2 == 0;
            return config;
        }

        private static ReInstallConfig GetInstallConfig()
        {
            var reInstall = new ReInstallConfig();
            reInstall.SetInstallPath(0, TestInfo.GetInstallPath(0));
            reInstall.SetInstallPath(1, TestInfo.GetInstallPath(1));
            reInstall.SetInstallPath(2, TestInfo.GetInstallPath(2));
            reInstall.SetEnabled(0, true);
            reInstall.SetEnabled(1, true);
            reInstall.SetEnabled(2, true);
            return reInstall;
        }

        private class NullProgress : IRandoProgress
        {
            public IDisposable BeginTask(int? player, string message)
            {
                return new NullDisposable();
            }

            private struct NullDisposable : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }
    }
}