using System;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.BioRand.RE2
{
    internal class Re2NpcHelper : INpcHelper
    {
        public string GetNpcName(byte type)
        {
            var name = new Bio2ConstantTable().GetEnemyName(type);
            return name
                .Remove(0, 6)
                .Replace("_", " ");
        }

        public string[] GetPlayerActors(int player)
        {
            return player == 0 ? new[] { "leon", "ada" } : new[] { "claire", "sherry" };
        }

        public byte[] GetDefaultIncludeTypes(RandomizedRdt rdt)
        {
            var defaultIncludeTypes = new byte[] {
                // Re2EnemyIds.ChiefIrons1,
                Re2EnemyIds.AdaWong1,
                // Re2EnemyIds.ChiefIrons2,
                Re2EnemyIds.AdaWong2,
                Re2EnemyIds.BenBertolucci1,
                Re2EnemyIds.SherryWithPendant,
                Re2EnemyIds.BenBertolucci2,
                Re2EnemyIds.AnnetteBirkin1,
                Re2EnemyIds.RobertKendo,
                Re2EnemyIds.AnnetteBirkin2,
                // Re2EnemyIds.MarvinBranagh,
                Re2EnemyIds.SherryWithClairesJacket,
                Re2EnemyIds.LeonKennedyRpd,
                Re2EnemyIds.ClaireRedfield,
                Re2EnemyIds.LeonKennedyCapTankTop,
                Re2EnemyIds.ClaireRedfieldCowGirl,
                Re2EnemyIds.LeonKennedyBlackLeather
            };
            return defaultIncludeTypes;
        }

        public bool IsNpc(byte type) => type >= Re2EnemyIds.ChiefIrons1 && type != Re2EnemyIds.MayorsDaughter;

        public string? GetActor(byte type)
        {
            switch (type)
            {
                case Re2EnemyIds.AdaWong1:
                case Re2EnemyIds.AdaWong2:
                    return "ada";
                case Re2EnemyIds.ClaireRedfield:
                case Re2EnemyIds.ClaireRedfieldCowGirl:
                case Re2EnemyIds.ClaireRedfieldNoJacket:
                    return "claire";
                case Re2EnemyIds.LeonKennedyBandaged:
                case Re2EnemyIds.LeonKennedyBlackLeather:
                case Re2EnemyIds.LeonKennedyCapTankTop:
                case Re2EnemyIds.LeonKennedyRpd:
                    return "leon";
                case Re2EnemyIds.SherryWithClairesJacket:
                case Re2EnemyIds.SherryWithPendant:
                    return "sherry";
                case Re2EnemyIds.MarvinBranagh:
                    return "marvin";
                case Re2EnemyIds.AnnetteBirkin1:
                case Re2EnemyIds.AnnetteBirkin2:
                    return "annette";
                case Re2EnemyIds.ChiefIrons1:
                case Re2EnemyIds.ChiefIrons2:
                    return "irons";
                case Re2EnemyIds.BenBertolucci1:
                case Re2EnemyIds.BenBertolucci2:
                    return "ben";
                case Re2EnemyIds.RobertKendo:
                    return "kendo";
                default:
                    return null;
            }
        }

        public byte[] GetSlots(RandoConfig config, byte id)
        {
            switch (id)
            {
                case Re2EnemyIds.LeonKennedyRpd:
                    return new byte[] { 0x48, 0x52, 0x54, 0x56, 0x58, 0x5A,
                        Re2EnemyIds.ChiefIrons1,
                        Re2EnemyIds.ChiefIrons2,
                        Re2EnemyIds.BenBertolucci1,
                        Re2EnemyIds.BenBertolucci2,
                        Re2EnemyIds.MarvinBranagh
                    };
                case Re2EnemyIds.ClaireRedfield:
                    return new byte[] { 0x53, 0x55, 0x57, 0x5B,
                        Re2EnemyIds.ChiefIrons1,
                        Re2EnemyIds.ChiefIrons2,
                        Re2EnemyIds.BenBertolucci1,
                        Re2EnemyIds.BenBertolucci2,
                        Re2EnemyIds.MarvinBranagh
                    };
                case Re2EnemyIds.SherryWithPendant:
                case Re2EnemyIds.SherryWithClairesJacket:
                    return new byte[] { 0x48, 0x52, 0x54, 0x56, 0x58, 0x5A,
                        Re2EnemyIds.SherryWithPendant,
                        Re2EnemyIds.SherryWithClairesJacket,
                        Re2EnemyIds.ChiefIrons1,
                        Re2EnemyIds.ChiefIrons2,
                        Re2EnemyIds.BenBertolucci1,
                        Re2EnemyIds.BenBertolucci2,
                        Re2EnemyIds.MarvinBranagh
                    };
                case Re2EnemyIds.AdaWong1:
                case Re2EnemyIds.AdaWong2:
                    return new byte[] {
                        Re2EnemyIds.AdaWong1,
                        Re2EnemyIds.AdaWong2
                    };
                case Re2EnemyIds.BenBertolucci1:
                case Re2EnemyIds.BenBertolucci2:
                case Re2EnemyIds.ChiefIrons1:
                case Re2EnemyIds.ChiefIrons2:
                    return new byte[] {
                        Re2EnemyIds.BenBertolucci1,
                        Re2EnemyIds.BenBertolucci1,
                        Re2EnemyIds.ChiefIrons1,
                        Re2EnemyIds.ChiefIrons2
                    };
                default:
                    return new[] { id };
            }
        }

        public bool IsSpareSlot(byte id)
        {
            switch (id)
            {
                case Re2EnemyIds.LeonKennedyRpd:
                case Re2EnemyIds.LeonKennedyBandaged:
                case Re2EnemyIds.LeonKennedyCapTankTop:
                case Re2EnemyIds.LeonKennedyBlackLeather:
                case Re2EnemyIds.ClaireRedfield:
                case Re2EnemyIds.ClaireRedfieldNoJacket:
                case Re2EnemyIds.ClaireRedfieldCowGirl:
                case Re2EnemyIds.RobertKendo:
                case Re2EnemyIds.AdaWong1:
                case Re2EnemyIds.AdaWong2:
                case Re2EnemyIds.AnnetteBirkin1:
                case Re2EnemyIds.AnnetteBirkin2:
                case Re2EnemyIds.MarvinBranagh:
                case Re2EnemyIds.SherryWithPendant:
                case Re2EnemyIds.SherryWithClairesJacket:
                case Re2EnemyIds.BenBertolucci1:
                case Re2EnemyIds.BenBertolucci2:
                case Re2EnemyIds.ChiefIrons1:
                case Re2EnemyIds.ChiefIrons2:
                case Re2EnemyIds.MayorsDaughter:
                    return true;
                default:
                    return false;
            }
        }

        public void CreateEmdFile(byte type, string pldPath, string baseEmdPath, string targetEmdPath, DataManager dataManager, FileRepository fileRepository, Rng rng)
        {
            var pldFile = new PldFile(BioVersion.Biohazard2, new MemoryStream(dataManager.GetData(pldPath)));
            var emdFile = ModelFile.FromFile(baseEmdPath);
            var timFile = pldFile.GetTim(0);
            var plwFile = null as ModelFile;

            if (timFile.Width != 128 * 4)
            {
                throw new BioRandUserException($"{pldPath} does not conform to the PLD texture standard.");
            }

            var weapon = GetSuitableWeaponForNPC(type).Random(rng);
            var plwPath = GetPlwPath(dataManager, fileRepository, pldPath, weapon);
            if (plwPath != null)
            {
                plwFile = new PlwFile(BioVersion.Biohazard2, new MemoryStream(dataManager.GetData(plwPath)));
                var plwTim = plwFile.GetTim(0);
                timFile = timFile.WithWeaponTexture(plwTim, 1);
                timFile = timFile.WithWeaponTexture(plwTim, 3);
            }

            // First get how tall the new EMD is compared to the old one
            var targetScale = pldFile.CalculateEmrScale(emdFile);

            // Now copy over the skeleton and scale the EMR keyframes
            if (type == Re2EnemyIds.MayorsDaughter)
            {
                var zombiePath = fileRepository.GetDataPath("pl0/emd0/em010.emd");
                var zombie = new EmdFile(BioVersion.Biohazard2, zombiePath);
                var edd = (Edd1)zombie.GetEdd(1);
                var eddBuilder = edd.ToBuilder();
                eddBuilder.Animations[0] = eddBuilder.Animations[22];

                var firstFrame = eddBuilder.Animations[0].Frames.First();
                eddBuilder.Animations[0].Frames.Clear();
                eddBuilder.Animations[0].Frames.Add(firstFrame);

                eddBuilder.Animations.RemoveRange(1, eddBuilder.Animations.Count - 1);
                emdFile.SetEdd(0, eddBuilder.ToEdd());
                var emr = pldFile.GetEmr(0)
                    .WithKeyframes(zombie.GetEmr(1))
                    .ToBuilder();

                var kf = emr.KeyFrames[eddBuilder.Animations[0].Frames[0].Index];
                var kfOffset = kf.Offset;
                kfOffset.x += 300;
                kf.Offset = kfOffset;
                var a = kf.Angles[0];
                a.y = 1024 * 3;
                kf.Angles[0] = a;

                emdFile.SetEmr(0, emr.ToEmr());
            }
            else if (type == Re2EnemyIds.RobertKendo)
            {
                // Copy skeleton and animations
                emdFile.SetEdd(0, pldFile.GetEdd(0));
                emdFile.SetEmr(0, pldFile.GetEmr(0).Scale(targetScale));
                emdFile.SetEdd(1, plwFile!.GetEdd(0));
                emdFile.SetEmr(1, emdFile.GetEmr(1).WithKeyframes(plwFile.GetEmr(0)).Scale(targetScale));
            }
            else
            {
                // Only copy skeleton, not animations
                emdFile.SetEmr(0, emdFile.GetEmr(0).WithSkeleton(pldFile.GetEmr(0)).Scale(targetScale));
                emdFile.SetEmr(1, emdFile.GetEmr(1).Scale(targetScale));
            }

            // Copy over the mesh (clear any extra parts)
            var builder = ((Md1)pldFile.GetMesh(0)).ToBuilder();
            var hairParts = builder.Parts.Skip(15).ToArray();
            if (builder.Parts.Count > 15)
                builder.Parts.RemoveRange(15, builder.Parts.Count - 15);

            // Add extra meshes
            var weaponMesh = builder.Parts[11];
            if (plwFile != null)
            {
                weaponMesh = ((Md1)plwFile.GetMesh(0)).ToBuilder().Parts[0];
            }

            if (type == Re2EnemyIds.ZombieBrad)
            {
                var zombieParts = new[] { 10, 0 };
                foreach (var zp in zombieParts)
                    builder.Parts.Add(builder.Parts[zp]);
            }
            else if (type == Re2EnemyIds.RobertKendo)
            {
                builder.Parts[11] = weaponMesh;
            }
            else if (type == Re2EnemyIds.MarvinBranagh)
            {
                var zombieParts = new[] { 13, 0, 8, 12, 14, 9, 10, 11, 11 };
                foreach (var zp in zombieParts)
                    builder.Parts.Add(builder.Parts[zp]);
                builder.Parts[builder.Parts.Count - 1] = weaponMesh;
            }
            else if (type == Re2EnemyIds.ChiefIrons1 || type == Re2EnemyIds.ChiefIrons2)
            {
                builder.Add(weaponMesh);
                builder.Add();
            }
            else if (type == Re2EnemyIds.BenBertolucci1 || type == Re2EnemyIds.BenBertolucci2 ||
                     type == Re2EnemyIds.SherryWithPendant || type == Re2EnemyIds.SherryWithClairesJacket)
            {
                builder.Add();
            }
            else if (type == Re2EnemyIds.AnnetteBirkin1 || type == Re2EnemyIds.AnnetteBirkin2)
            {
                builder.Add(weaponMesh);
            }
            else if (type == Re2EnemyIds.AdaWong1 || type == Re2EnemyIds.AdaWong2)
            {
                builder[0].InsertDummyPoints(160);
                builder.Add(weaponMesh);
                builder.Add();
            }
            else if (type == Re2EnemyIds.LeonKennedyBlackLeather)
            {
                builder[0].InsertDummyPoints(146);
            }
            else if (type == Re2EnemyIds.ClaireRedfield ||
                     type == Re2EnemyIds.ClaireRedfieldNoJacket ||
                     type == Re2EnemyIds.ClaireRedfieldCowGirl)
            {
                for (var i = 0; i < 4; i++)
                {
                    if (i < hairParts.Length)
                    {
                        builder.Add(hairParts[i]);
                    }
                }
                while (builder.Count < 15 + 4)
                {
                    builder.Add();
                }
            }
            else if (type == Re2EnemyIds.MayorsDaughter)
            {
                // Offset body to right place above table
                foreach (var part in builder.Parts)
                {
                    for (var i = 0; i < part.Positions.Count; i++)
                    {
                        var p = part.Positions[i];
                        p.x += 100;
                        part.Positions[i] = p;
                    }
                }
            }

            emdFile.SetMesh(0, builder.ToMesh());

            // Marvin
            if (type == Re2EnemyIds.MarvinBranagh)
            {
                emdFile.SetMesh(0, emdFile.GetMesh(0).EditMeshTextures(m =>
                {
                    if (m.PartIndex >= 15 && m.PartIndex != 23)
                    {
                        m.Page += 2;
                    }
                    else if (m.Page == 0)
                    {
                        m.Page += 2;
                    }
                }));
            }
            else if (type == Re2EnemyIds.MayorsDaughter)
            {
                emdFile.SetMesh(0, emdFile.GetMesh(0).EditMeshTextures(m =>
                {
                    m.Page += 2;
                }));
            }

            // Ben and Irons need have morphing info that needs zeroing
            if (type == Re2EnemyIds.ChiefIrons1 ||
                type == Re2EnemyIds.BenBertolucci1 || type == Re2EnemyIds.BenBertolucci2)
            {
                var morph = emdFile.GetMorph(0).ToBuilder();

                // Copy skeleton from EMR
                var emr = emdFile.GetEmr(0);
                var skel = Enumerable.Range(0, 15).Select(x => emr.GetRelativePosition(x)).ToArray();
                for (var i = 0; i < morph.Skeletons.Count; i++)
                {
                    morph.Skeletons[i] = skel;
                }

                // Copy positions from chest mesh to morph group 0
                var positionData = ((Md1)emdFile.GetMesh(0))
                    .ToBuilder().Parts[0].Positions
                    .Select(p => new Emr.Vector(p.x, p.y, p.z))
                    .ToArray();
                for (var i = 0; i < morph.Groups[0].Positions.Count; i++)
                {
                    morph.Groups[0].Positions[i] = positionData;
                }

                // Morph group 1 can just be zeros
                for (var i = 0; i < morph.Groups[1].Positions.Count; i++)
                {
                    morph.Groups[1].Positions[i] = new Emr.Vector[1];
                }

                emdFile.SetMorph(0, morph.ToMorphData());
            }

            // Ben and Irons need to have their chest on the right texture page
            if (type == Re2EnemyIds.ChiefIrons1 || type == Re2EnemyIds.ChiefIrons2 ||
                type == Re2EnemyIds.BenBertolucci1 || type == Re2EnemyIds.BenBertolucci2)
            {
                var pageIndex = type == Re2EnemyIds.ChiefIrons1 || type == Re2EnemyIds.ChiefIrons2 ? 0 : 1;
                var mesh = (Md1)emdFile.GetMesh(0);
                if (EnsureChestOnPage(ref mesh, pageIndex))
                {
                    mesh = (Md1)mesh.EditMeshTextures(m =>
                    {
                        if (m.PartIndex == 0 || m.PartIndex == 9 || m.PartIndex == 12)
                        {
                            m.Page = pageIndex ^ 1;
                        }
                    });
                    timFile.SwapPages(0, 1);
                    emdFile.SetMesh(0, mesh);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetEmdPath));
            emdFile.Save(targetEmdPath);
            timFile.Save(Path.ChangeExtension(targetEmdPath, ".tim"));
        }

        private static string? GetPlwPath(DataManager dataManager, FileRepository fileRepository, string pldPath, byte weapon)
        {
            var player = 0;
            var fileName = Path.GetFileNameWithoutExtension(pldPath);
            if (fileName.Equals("pl01", StringComparison.OrdinalIgnoreCase))
            {
                player = 1;
            }

            var plwFileName = $"PL{player:00}W{weapon:X2}.PLW";
            var pldDirectory = Path.GetDirectoryName(pldPath);
            var customPlwPath = Path.Combine(pldDirectory, plwFileName);
            if (dataManager.GetData(customPlwPath) != null)
            {
                return customPlwPath;
            }

            var originalPlwPath = fileRepository.GetDataPath($"pl{player}/pld/{plwFileName}");
            if (File.Exists(originalPlwPath))
            {
                return originalPlwPath;
            }

            return null;
        }

        private static byte[] GetSuitableWeaponForNPC(byte npc)
        {
            return npc switch
            {
                Re2EnemyIds.MarvinBranagh => new[]
                {
                    Re2ItemIds.HandgunLeon,
                    Re2ItemIds.Magnum,
                    Re2ItemIds.ColtSAA,
                    Re2ItemIds.Shotgun,
                    Re2ItemIds.Bowgun,
                    Re2ItemIds.GrenadeLauncherExplosive,
                    Re2ItemIds.Sparkshot,
                    Re2ItemIds.SMG,
                    Re2ItemIds.Flamethrower,
                    Re2ItemIds.RocketLauncher,
                },
                Re2EnemyIds.RobertKendo => new[]
                {
                    Re2ItemIds.Shotgun,
                    Re2ItemIds.Bowgun,
                    Re2ItemIds.GrenadeLauncherExplosive,
                    Re2ItemIds.Sparkshot,
                    Re2ItemIds.SMG,
                    Re2ItemIds.Flamethrower,
                    Re2ItemIds.RocketLauncher,
                },
                _ => new[]
                {
                    Re2ItemIds.HandgunLeon,
                    Re2ItemIds.Magnum,
                    Re2ItemIds.ColtSAA
                },
            };
        }

        private static bool EnsureChestOnPage(ref Md1 mesh, int page)
        {
            var builder = mesh.ToBuilder();
            var part0 = builder.Parts[0];
            if (part0.TriangleTextures.Count > 0)
            {
                if ((part0.TriangleTextures[0].page & 0x0F) == page)
                {
                    return false;
                }
            }
            else if (part0.QuadTextures.Count > 0)
            {
                if ((part0.QuadTextures[0].page & 0x0F) == page)
                {
                    return false;
                }
            }
            mesh = (Md1)mesh.SwapPages(0, 1);
            return true;
        }
    }
}
