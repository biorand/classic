using System;
using System.IO;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.BioRand.RE3
{
    internal class Re3NpcHelper : INpcHelper
    {
        public string? GetActor(byte type)
        {
            switch (type)
            {
                case Re3EnemyIds.MarvinBranagh1:
                case Re3EnemyIds.MarvinBranagh2:
                    return "marvin";
                case Re3EnemyIds.MikhailViktor:
                    return "mikhail";
                case Re3EnemyIds.NikolaiZinoviev:
                case Re3EnemyIds.NikolaiDead:
                    return "nikolai";
                case Re3EnemyIds.BradVickers:
                case Re3EnemyIds.BradZombie:
                    return "brad";
                case Re3EnemyIds.DarioRosso:
                case Re3EnemyIds.DarioZombie:
                    return "dario";
                case Re3EnemyIds.MurphySeeker:
                    return "murphy";
                case Re3EnemyIds.TyrellPatrick:
                    return "tyrell";
                case Re3EnemyIds.CarlosOliveira1:
                case Re3EnemyIds.CarlosOliveira2:
                    return "carlos";
                case Re3EnemyIds.PromoGirl:
                    return "promogirl";
                case Re3EnemyIds.JillValentine1:
                case Re3EnemyIds.JillValentine2:
                    return "jill.re3";
                case Re3EnemyIds.ChiefIrons:
                    return "irons";
                default:
                    return null;
            }
        }

        public string GetNpcName(byte type)
        {
            var name = new Bio3ConstantTable().GetEnemyName(type);
            return name
                .Remove(0, 6)
                .Replace("_", " ");
        }

        public string[] GetPlayerActors(int player)
        {
            return new[] { "jill.re3", "carlos" };
        }

        public bool IsNpc(byte type)
        {
            return type >= Re3EnemyIds.CarlosOliveira1 || type == Re3EnemyIds.MarvinBranagh1;
        }

        public byte[] GetDefaultIncludeTypes(RandomizedRdt rdt)
        {
            var defaultIncludeTypes = new byte[] {
                Re3EnemyIds.MarvinBranagh1,
                Re3EnemyIds.MarvinBranagh2,
                Re3EnemyIds.NikolaiZinoviev,
                Re3EnemyIds.BradVickers,
                Re3EnemyIds.TyrellPatrick,
                // Re3EnemyIds.PromoGirl,
                Re3EnemyIds.CarlosOliveira1,
                Re3EnemyIds.CarlosOliveira2,
                Re3EnemyIds.JillValentine1,
                Re3EnemyIds.JillValentine2,
                Re3EnemyIds.ChiefIrons
            };
            return defaultIncludeTypes;
        }

        public byte[] GetSlots(RandoConfig config, byte id)
        {
            switch (id)
            {
                case Re3EnemyIds.CarlosOliveira1:
                    return new byte[] {
                        Re3EnemyIds.MarvinBranagh1,
                        Re3EnemyIds.CarlosOliveira1,
                        Re3EnemyIds.NikolaiZinoviev,
                        Re3EnemyIds.BradVickers,
                        Re3EnemyIds.DarioRosso,
                        Re3EnemyIds.MurphySeeker,
                        Re3EnemyIds.TyrellPatrick,
                        Re3EnemyIds.MarvinBranagh2,
                        Re3EnemyIds.BradZombie,
                        Re3EnemyIds.DarioZombie,
                        Re3EnemyIds.PromoGirl,
                        Re3EnemyIds.NikolaiDead,
                        Re3EnemyIds.ChiefIrons
                    };
                default:
                    return new[] { id };
            }
        }

        public bool IsSpareSlot(byte id)
        {
            switch (id)
            {
                // Simple
                case Re3EnemyIds.MarvinBranagh1:
                case Re3EnemyIds.MarvinBranagh2:
                case Re3EnemyIds.PromoGirl:
                case Re3EnemyIds.ChiefIrons:

                // Advanced
                case Re3EnemyIds.CarlosOliveira1:
                case Re3EnemyIds.MikhailViktor:
                case Re3EnemyIds.NikolaiZinoviev:
                case Re3EnemyIds.BradVickers:
                case Re3EnemyIds.DarioRosso:
                case Re3EnemyIds.MurphySeeker:
                case Re3EnemyIds.TyrellPatrick:
                case Re3EnemyIds.BradZombie:
                case Re3EnemyIds.DarioZombie:
                // case Re3EnemyIds.JillValentine1:
                case Re3EnemyIds.CarlosOliveira2:
                // case Re3EnemyIds.JillValentine2:
                case Re3EnemyIds.NikolaiDead:
                    return true;
                default:
                    return false;
            }
        }

        public void CreateEmdFile(byte type, string pldPath, string baseEmdPath, string targetEmdPath, DataManager dataManager, FileRepository fileRepository, Rng rng)
        {
            var pldFile = new PldFile(BioVersion.Biohazard3, new MemoryStream(dataManager.GetData(pldPath)));
            var emdFile = new EmdFile(BioVersion.Biohazard3, fileRepository.GetStream(baseEmdPath));
            var timFile = pldFile.GetTim(0);
            var plwFile = null as ModelFile;

            var weapon = GetSuitableWeaponForNPC(type).Random(rng);
            var plwPath = GetPlwPath(fileRepository, pldPath, weapon);
            if (plwPath != null)
            {
                plwFile = new PlwFile(BioVersion.Biohazard3, fileRepository.GetStream(plwPath));
                var plwTim = plwFile.GetTim(0);
                timFile = timFile.WithWeaponTexture(plwTim, 1);
            }

            // First get how tall the new EMD is compared to the old one
            var targetScale = pldFile.CalculateEmrScale(emdFile);

            emdFile.SetEmr(0, emdFile.GetEmr(0).WithSkeleton(pldFile.GetEmr(0)).Scale(targetScale));
            emdFile.SetEmr(1, emdFile.GetEmr(1).Scale(targetScale));

            // Copy over the mesh (clear any extra parts)
            var builder = ((Md2)pldFile.GetMesh(0)).ToBuilder();
            if (builder.Parts.Count > 15)
                builder.Parts.RemoveRange(15, builder.Parts.Count - 15);

            // Add extra meshes
            var weaponMesh = builder.Parts[4];
            if (plwFile != null)
            {
                weaponMesh = ((Md2)plwFile.GetMesh(0)).ToBuilder().Parts[0];
            }

            if (HasWeapon(type))
            {
                builder.Add(weaponMesh);
            }
            else if (type == Re3EnemyIds.JillValentine1 || type == Re3EnemyIds.JillValentine2)
            {
                builder.Add();
            }

            emdFile.SetMesh(1, builder.ToMesh());

            emdFile.SetChunk(0, pldFile.GetChunk<MorphData>(3));

            // The texture should only be two pages
            timFile.ResizeImage(2 * 128, 256);
            timFile.ResizeCluts(2);

            Directory.CreateDirectory(Path.GetDirectoryName(targetEmdPath));
            emdFile.Save(targetEmdPath);
            timFile.Save(Path.ChangeExtension(targetEmdPath, ".tim"));
        }

        private static string? GetPlwPath(FileRepository fileRepository, string pldPath, byte weapon)
        {
            var plwFileName = $"PL00W{weapon:X2}.PLW";
            var pldDirectory = Path.GetDirectoryName(pldPath);
            var customPlwPath = Path.Combine(pldDirectory, plwFileName);
            if (File.Exists(customPlwPath))
            {
                return customPlwPath;
            }

            var originalPlwPath = fileRepository.GetDataPath($"data/pld/{plwFileName}");
            if (fileRepository.Exists(originalPlwPath))
            {
                return originalPlwPath;
            }

            return null;
        }

        private static byte[] GetSuitableWeaponForNPC(byte npc)
        {
            switch (npc)
            {
                case Re3EnemyIds.MikhailViktor:
                case Re3EnemyIds.CarlosOliveira2:
                    return new[]
                    {
                        Re3ItemIds.RifleM4A1Auto
                    };
                default:
                    return new[]
                    {
                        Re3ItemIds.HandgunSigpro,
                        Re3ItemIds.HandgunBeretta,
                        Re3ItemIds.ShotgunBenelli,
                        Re3ItemIds.MagnumSW,
                        Re3ItemIds.GrenadeLauncherGrenade,
                        Re3ItemIds.GrenadeLauncherFlame,
                        Re3ItemIds.GrenadeLauncherAcid,
                        Re3ItemIds.GrenadeLauncherFreeze,
                        Re3ItemIds.RocketLauncher,
                        Re3ItemIds.GatlingGun,
                        Re3ItemIds.MineThrower,
                        Re3ItemIds.HangunEagle,
                        Re3ItemIds.RifleM4A1Manual,
                        Re3ItemIds.RifleM4A1Auto,
                        Re3ItemIds.ShotgunM37,
                        Re3ItemIds.HandgunSigproEnhanced,
                        Re3ItemIds.HandgunBerettaEnhanced,
                        Re3ItemIds.ShotgunBenelliEnhanced,
                        Re3ItemIds.MineThrowerEnhanced,
                    };
            }
            ;
        }

        private static bool HasWeapon(byte type)
        {
            switch (type)
            {
                case Re3EnemyIds.CarlosOliveira1:
                case Re3EnemyIds.MikhailViktor:
                case Re3EnemyIds.NikolaiZinoviev:
                case Re3EnemyIds.BradVickers:
                case Re3EnemyIds.TyrellPatrick:
                case Re3EnemyIds.CarlosOliveira2:
                    return true;
                default:
                    return false;
            }
        }

        private static bool HasGore(byte type)
        {
            switch (type)
            {
                case Re3EnemyIds.BradZombie:
                case Re3EnemyIds.DarioZombie:
                case Re3EnemyIds.NikolaiDead:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsMorphingNpc(byte type)
        {
            switch (type)
            {
                case Re3EnemyIds.CarlosOliveira1:
                case Re3EnemyIds.MikhailViktor:
                case Re3EnemyIds.NikolaiZinoviev:
                case Re3EnemyIds.BradVickers:
                case Re3EnemyIds.DarioRosso:
                case Re3EnemyIds.MurphySeeker:
                case Re3EnemyIds.TyrellPatrick:
                case Re3EnemyIds.BradZombie:
                case Re3EnemyIds.DarioZombie:
                case Re3EnemyIds.JillValentine1:
                case Re3EnemyIds.CarlosOliveira2:
                case Re3EnemyIds.JillValentine2:
                case Re3EnemyIds.NikolaiDead:
                    return true;
                case Re3EnemyIds.MarvinBranagh1:
                case Re3EnemyIds.MarvinBranagh2:
                case Re3EnemyIds.PromoGirl:
                case Re3EnemyIds.ChiefIrons:
                default:
                    return false;
            }
        }
    }
}
