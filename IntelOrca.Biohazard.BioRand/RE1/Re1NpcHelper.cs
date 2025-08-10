using System.IO;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.BioRand.RE1
{
    internal class Re1NpcHelper : INpcHelper
    {
        public string GetNpcName(byte type)
        {
            var name = new Bio1ConstantTable().GetEnemyName(type);
            return name
                .Remove(0, 6)
                .Replace("_", " ");
        }

        public byte[] GetDefaultIncludeTypes(RandomizedRdt rdt)
        {
            return new byte[]
            {
                Re1EnemyIds.ChrisStars,
                Re1EnemyIds.JillStars,
                Re1EnemyIds.BarryStars,
                Re1EnemyIds.RebeccaStars,
                Re1EnemyIds.WeskerStars,
                // Re1EnemyIds.Kenneth1,
                // Re1EnemyIds.Forrest,
                Re1EnemyIds.Richard,
                Re1EnemyIds.Enrico,
                // Re1EnemyIds.Kenneth2,
                // Re1EnemyIds.Barry2,
                // Re1EnemyIds.BarryStars2,
                // Re1EnemyIds.RebeccaStars2,
                // Re1EnemyIds.WeskerStars2,
                // Re1EnemyIds.ChrisJacket,
                // Re1EnemyIds.JillBlackShirt,
                // Re1EnemyIds.ChrisJacket2,
                // Re1EnemyIds.JillRedShirt
            };
        }

        public string[] GetPlayerActors(int player)
        {
            return player == 0 ? new[] { "chris", "rebecca" } : new[] { "jill", "barry" };
        }

        public bool IsNpc(byte type) => type >= Re1EnemyIds.ChrisStars;

        public string? GetActor(byte type)
        {
            switch (type)
            {
                case Re1EnemyIds.ChrisStars:
                case Re1EnemyIds.ChrisJacket:
                case Re1EnemyIds.ChrisJacket2:
                    return "chris";
                case Re1EnemyIds.JillStars:
                case Re1EnemyIds.JillBlackShirt:
                case Re1EnemyIds.JillRedShirt:
                    return "jill";
                case Re1EnemyIds.BarryStars:
                case Re1EnemyIds.Barry2:
                case Re1EnemyIds.BarryStars2:
                case Re1EnemyIds.Barry3:
                    return "barry";
                case Re1EnemyIds.RebeccaStars:
                case Re1EnemyIds.RebeccaStars2:
                    return "rebecca";
                case Re1EnemyIds.WeskerStars:
                case Re1EnemyIds.WeskerStars2:
                    return "wesker";
                case Re1EnemyIds.Kenneth1:
                case Re1EnemyIds.Kenneth2:
                    return "kenneth";
                case Re1EnemyIds.Forrest:
                    return "forrest";
                case Re1EnemyIds.Richard:
                    return "richard";
                case Re1EnemyIds.Enrico:
                    return "enrico";
                default:
                    return null;
            }
        }

        public byte[] GetSlots(RandoConfig config, byte id)
        {
            if (id == Re1EnemyIds.ChrisStars)
            {
                return new[]
                {
                    Re1EnemyIds.ChrisStars,
                    Re1EnemyIds.JillStars,
                    Re1EnemyIds.BarryStars,
                    Re1EnemyIds.RebeccaStars,
                    Re1EnemyIds.WeskerStars
                };
            }
            else
            {
                return new[] { id };
            }
        }

        public bool IsSpareSlot(byte id)
        {
            switch (id)
            {
                case Re1EnemyIds.ChrisStars:
                case Re1EnemyIds.JillStars:
                case Re1EnemyIds.BarryStars:
                case Re1EnemyIds.RebeccaStars:
                case Re1EnemyIds.WeskerStars:
                case Re1EnemyIds.Kenneth1:
                case Re1EnemyIds.Forrest:
                case Re1EnemyIds.Richard:
                case Re1EnemyIds.Enrico:
                case Re1EnemyIds.Kenneth2:
                case Re1EnemyIds.Barry2:
                case Re1EnemyIds.BarryStars2:
                case Re1EnemyIds.RebeccaStars2:
                case Re1EnemyIds.Barry3:
                case Re1EnemyIds.WeskerStars2:
                    return true;
                default:
                    return false;
            }
        }

        public void CreateEmdFile(byte type, string pldPath, string baseEmdPath, string targetEmdPath, DataManager dataManager, FileRepository fileRepository, Rng rng)
        {
            var pldFile = new EmdFile(BioVersion.Biohazard1, new MemoryStream(dataManager.GetData(pldPath)));
            var emdFile = ModelFile.FromFile(baseEmdPath);
            var timFile = pldFile.GetTim(0);

            // First get how tall the new EMD is compared to the old one
            var targetScale = pldFile.CalculateEmrScale(emdFile);
            switch (type)
            {
                case Re1EnemyIds.Kenneth1:
                case Re1EnemyIds.Forrest:
                case Re1EnemyIds.Richard:
                case Re1EnemyIds.Enrico:
                case Re1EnemyIds.Kenneth2:
                case Re1EnemyIds.Barry2:
                    targetScale = 1;
                    break;
            }

            // Now copy over the skeleton and scale the EMR keyframes
            emdFile.SetEmr(0, emdFile.GetEmr(0).WithSkeleton(pldFile.GetEmr(0)).Scale(targetScale));

            // Copy over the mesh (clear any extra parts)
            var builder = ((Tmd)pldFile.GetMesh(0)).ToBuilder();
            if (builder.Parts.Count > 15)
                builder.Parts.RemoveRange(15, builder.Parts.Count - 15);

            // Add clip part (probably unused)
            switch (type)
            {
                case Re1EnemyIds.ChrisStars:
                case Re1EnemyIds.JillStars:
                case Re1EnemyIds.RebeccaStars:
                    builder.Add();
                    break;
            }

            // Enrico has a baked in gun
            if (type == Re1EnemyIds.Enrico)
            {
                var plwIndex = rng.Next(Re1ItemIds.CombatKnife, Re1ItemIds.RocketLauncher + 1);
                var plwPath = GetPlwPath(pldPath, plwIndex, fileRepository);
                if (plwPath != null)
                {
                    var plwFile = new PlwFile(BioVersion.Biohazard1, plwPath);
                    builder[14] = plwFile.GetMesh(0).ToBuilder()[0];
                }
            }

            emdFile.SetMesh(0, builder.ToMesh());
            emdFile.SetTim(0, timFile);

            // Weapons
            var weapons = GetWeaponForCharacter(type);
            foreach (var (weapon, file) in weapons)
            {
                var plwIndex = GetWeapon(weapon);
                if (plwIndex == 0)
                    continue;

                var plwPath = GetPlwPath(pldPath, plwIndex, fileRepository);
                if (plwPath == null)
                    continue;

                var plwFile = new PlwFile(BioVersion.Biohazard1, plwPath);
                var mesh = plwFile.GetMesh(0);
                var weaponPath = fileRepository.GetModPath(Path.Combine("players", file));
                Directory.CreateDirectory(Path.GetDirectoryName(weaponPath));
                File.WriteAllBytes(weaponPath, mesh.Data.ToArray());
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetEmdPath));
            emdFile.Save(targetEmdPath);
        }

        private static string? GetPlwPath(string pldPath, int plwIndex, FileRepository fileRepository)
        {
            var playerIndex = 0;
            if (Path.GetFileName(pldPath).Equals("CHAR11.EMD", System.StringComparison.OrdinalIgnoreCase))
            {
                playerIndex = 1;
            }

            var plwFileName = $"W{playerIndex}{plwIndex}.EMW";
            var plwPath = Path.Combine(Path.GetDirectoryName(pldPath), plwFileName);
            if (File.Exists(plwPath))
            {
                return plwPath;
            }

            var originalPath = fileRepository.GetDataPath(Path.Combine("players", plwFileName));
            if (File.Exists(originalPath))
            {
                return originalPath;
            }

            return null;
        }

        private static (byte, string)[] GetWeaponForCharacter(byte type)
        {
            return type switch
            {
                Re1EnemyIds.ChrisStars => new[] { (Re1ItemIds.Beretta, "ws202.tmd") },
                Re1EnemyIds.JillStars => new[] { (Re1ItemIds.Beretta, "ws212.tmd") },
                Re1EnemyIds.BarryStars => new[] {
                    (Re1ItemIds.ColtPython, "ws224.tmd"),
                    (Re1ItemIds.FlameThrower, "ws225.tmd")
                },
                Re1EnemyIds.RebeccaStars => new[] {
                    (Re1ItemIds.Beretta, "ws232.tmd")
                },
                // (Re1ItemIds.FAidSpray, "ws236.tmd");
                Re1EnemyIds.WeskerStars => new[] { (Re1ItemIds.Beretta, "ws242.tmd") },
                _ => new (byte, string)[0],
            };
        }

        private static byte GetWeapon(byte type)
        {
            switch (type)
            {
                case Re1ItemIds.CombatKnife:
                    return 1;
                case Re1ItemIds.Beretta:
                    return 2;
                case Re1ItemIds.Shotgun:
                    return 3;
                case Re1ItemIds.ColtPython:
                    return 4;
                case Re1ItemIds.FlameThrower:
                    return 5;
                case Re1ItemIds.BazookaAcid:
                case Re1ItemIds.BazookaExplosive:
                case Re1ItemIds.BazookaFlame:
                    return 6;
                case Re1ItemIds.RocketLauncher:
                    return 7;
                case Re1ItemIds.MiniGun:
                    return 8;
                default:
                    return 0;
            }
            ;
        }
    }
}
