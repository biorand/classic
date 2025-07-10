using System.IO;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;

namespace IntelOrca.Biohazard.BioRand.RE1
{
    internal class Re1CharacterGenerator(
        DataManager biorandDataManager,
        DataManager gameDataManager,
        ClassicRebirthModBuilder crModBuilder)
    {
        public void Generate(byte emdId, CharacterReplacement cr)
        {
            var char10data = biorandDataManager.GetData($"{cr.Path}/char10.emd");
            var char11data = biorandDataManager.GetData($"{cr.Path}/char11.emd");
            var charData = char10data ?? char11data;
            var playerIndex = char10data != null ? 0 : 1;
            var pldFile = new EmdFile(BioVersion.Biohazard1, new MemoryStream(charData));
            var emdFile = GetBaseEmd(emdId);
            var timFile = pldFile.GetTim(0);

            // First get how tall the new EMD is compared to the old one
            var targetScale = pldFile.CalculateEmrScale(emdFile);
            switch (emdId)
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
            switch (emdId)
            {
                case Re1EnemyIds.ChrisStars:
                case Re1EnemyIds.JillStars:
                case Re1EnemyIds.RebeccaStars:
                    builder.Add();
                    break;
            }

            // Enrico has a baked in gun
            if (emdId == Re1EnemyIds.Enrico)
            {
                var plwFile = GetPlw((byte)cr.Weapon);
                if (plwFile != null)
                {
                    builder[14] = plwFile.GetMesh(0).ToBuilder()[0];
                }
            }

            emdFile.SetMesh(0, builder.ToMesh());
            emdFile.SetTim(0, timFile);

            // Weapons
            var weapons = GetWeaponForCharacter(emdId);
            foreach (var (weapon, file) in weapons)
            {
                var plwFile = GetPlw(weapon);
                if (plwFile == null)
                    continue;

                var mesh = plwFile.GetMesh(0);
                crModBuilder.SetFile(file, mesh.Data);
            }

            var ms = new MemoryStream();
            emdFile.Save(ms);
            crModBuilder.SetFile($"ENEMY/EM1{emdId:X3}.EMD", ms.ToArray());

            PlwFile? GetPlw(byte weaponItemId)
            {
                var plwIndex = GetWeapon(weaponItemId);
                var plwFileName = $"W{playerIndex}{plwIndex}.EMW";
                var plwPath = Path.Combine(cr.Path, plwFileName);
                if (File.Exists(plwPath))
                {
                    return new PlwFile(BioVersion.Biohazard1, plwPath);
                }

                var originalData = gameDataManager.GetData($"JPN/PLAYERS/{plwFileName}");
                if (originalData != null)
                {
                    return new PlwFile(BioVersion.Biohazard1, new MemoryStream(originalData));
                }

                return null;
            }
        }

        private ModelFile GetBaseEmd(byte id)
        {
            var data = gameDataManager.GetData($"JPN/ENEMY/EM10{id:X2}.EMD");
            return new EmdFile(BioVersion.Biohazard1, new MemoryStream(data));
        }

        private static (byte, string)[] GetWeaponForCharacter(byte type)
        {
            return type switch
            {
                Re1EnemyIds.ChrisStars => new[] { (Re1ItemIds.Beretta, "PLAYERS/WS202.TMD") },
                Re1EnemyIds.JillStars => [(Re1ItemIds.Beretta, "PLAYERS/WS212.TMD")],
                Re1EnemyIds.BarryStars => [
                    (Re1ItemIds.ColtPython, "PLAYERS/WS224.TMD"),
                    (Re1ItemIds.FlameThrower, "PLAYERS/WS225.TMD")
                ],
                Re1EnemyIds.RebeccaStars => [
                    (Re1ItemIds.Beretta, "PLAYERS/WS232.TMD")
                ],
                // (Re1ItemIds.FAidSpray, "ws236.tmd");
                Re1EnemyIds.WeskerStars => [(Re1ItemIds.Beretta, "PLAYERS/WS242.TMD")],
                _ => [],
            };
        }

        private static byte GetWeapon(byte type)
        {
            return type switch
            {
                Re1ItemIds.CombatKnife => 1,
                Re1ItemIds.Beretta => 2,
                Re1ItemIds.Shotgun => 3,
                Re1ItemIds.ColtPython => 4,
                Re1ItemIds.FlameThrower => 5,
                Re1ItemIds.BazookaAcid or Re1ItemIds.BazookaExplosive or Re1ItemIds.BazookaFlame => 6,
                Re1ItemIds.RocketLauncher => 7,
                Re1ItemIds.MiniGun => 8,
                _ => 0,
            };
        }
    }
}
