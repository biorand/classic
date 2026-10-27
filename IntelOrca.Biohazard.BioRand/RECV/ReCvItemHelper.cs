using System;

namespace IntelOrca.Biohazard.BioRand.RECV
{
    internal class ReCvItemHelper : IItemHelper
    {
        public byte[] GetAmmoTypeForWeapon(byte type)
        {
            switch (type)
            {
                case ReCvItemIds.Handgun:
                case ReCvItemIds.HandgunGlock17:
                    return new[] { ReCvItemIds.HandgunBullets };
                case ReCvItemIds.BowGun:
                    return new[] { ReCvItemIds.BowGunArrows, ReCvItemIds.GunPowderArrow };
                case ReCvItemIds.AssaultRifle:
                    return new[] { ReCvItemIds.ARifleBullets };
                case ReCvItemIds.SniperRifle:
                    return new[] { ReCvItemIds.RifleBullets };
                case ReCvItemIds.Shotgun:
                    return new[] { ReCvItemIds.ShotgunShells };
                case ReCvItemIds.GrenadeLauncher:
                    return new[] {
                        ReCvItemIds.AcidRounds,
                        ReCvItemIds.FlameRounds,
                        ReCvItemIds.GrenadeRounds,
                        ReCvItemIds.BOWGasRounds
                    };
                case ReCvItemIds.Magnum:
                    return new[] { ReCvItemIds.MagnumBullets };
                default:
                    return new byte[0];
            }
        }

        public byte[] GetDefaultWeapons(RandoConfig config)
        {
            // return new[] { ReCvItemIds.Handgun };
            return new byte[0];
        }

        public string GetFriendlyItemName(byte type)
        {
            return GetItemName(type);
        }

        public byte[] GetInitialKeyItems(RandoConfig config)
        {
            return new[] { ReCvItemIds.Lighter };
        }

        public int[]? GetInventorySize(RandoConfig config)
        {
            return new[] { 8 };
        }

        public ItemAttribute GetItemAttributes(byte item)
        {
            switch (item)
            {
                case ReCvItemIds.RocketLauncher:
                case ReCvItemIds.AssaultRifle:
                case ReCvItemIds.SniperRifle:
                case ReCvItemIds.Shotgun:
                case ReCvItemIds.HandgunGlock17:
                case ReCvItemIds.GrenadeLauncher:
                case ReCvItemIds.BowGun:
                case ReCvItemIds.CombatKnife:
                case ReCvItemIds.Handgun:
                case ReCvItemIds.CustomHandgun:
                case ReCvItemIds.LinearLauncher:
                    return ItemAttribute.Weapon;
                case ReCvItemIds.HandgunBullets:
                case ReCvItemIds.MagnumBullets:
                case ReCvItemIds.ShotgunShells:
                case ReCvItemIds.GrenadeRounds:
                case ReCvItemIds.AcidRounds:
                case ReCvItemIds.FlameRounds:
                case ReCvItemIds.BowGunArrows:
                    return ItemAttribute.Ammo;
                case ReCvItemIds.M93RPart:
                    return ItemAttribute.Weapon;
                case ReCvItemIds.FAidSpray:
                case ReCvItemIds.GreenHerb:
                case ReCvItemIds.RedHerb:
                case ReCvItemIds.BlueHerb:
                case ReCvItemIds.MixedHerb2Green:
                case ReCvItemIds.MixedHerbRedGreen:
                case ReCvItemIds.MixedHerbBlueGreen:
                case ReCvItemIds.MixedHerb2GreenBlue:
                case ReCvItemIds.MixedHerb3Green:
                case ReCvItemIds.MixedHerbGreenBlueRed:
                    return ItemAttribute.Heal;
                case ReCvItemIds.MagnumBulletsInsideCase:
                    return ItemAttribute.Ammo;
                case ReCvItemIds.InkRibbon:
                    return ItemAttribute.InkRibbon;
                case ReCvItemIds.Magnum:
                    return ItemAttribute.Weapon;
                case ReCvItemIds.GoldLugers:
                    return ItemAttribute.Key | ItemAttribute.Weapon;
                case ReCvItemIds.SubMachineGun:
                    return ItemAttribute.Weapon;
                case ReCvItemIds.BowGunPowder:
                case ReCvItemIds.GunPowderArrow:
                    return ItemAttribute.Gunpowder;
                case ReCvItemIds.BOWGasRounds:
                case ReCvItemIds.MGunBullets:
                    return ItemAttribute.Ammo;
                case ReCvItemIds.GasMask:
                    return ItemAttribute.Key;
                case ReCvItemIds.RifleBullets:
                    return ItemAttribute.Ammo;
                case ReCvItemIds.DuraluminCaseUnused:
                    return 0;
                case ReCvItemIds.ARifleBullets:
                    return ItemAttribute.Ammo;
                case ReCvItemIds.AlexandersPierce:
                case ReCvItemIds.AlexandersJewel:
                case ReCvItemIds.AlfredsRing:
                case ReCvItemIds.AlfredsJewel:
                    return ItemAttribute.Key;
                case ReCvItemIds.PrisonersDiary:
                case ReCvItemIds.DirectorsMemo:
                case ReCvItemIds.Instructions:
                    return ItemAttribute.Document;
                case ReCvItemIds.Lockpick:
                case ReCvItemIds.GlassEye:
                case ReCvItemIds.PianoRoll:
                case ReCvItemIds.SteeringWheel:
                case ReCvItemIds.CraneKey:
                case ReCvItemIds.Lighter:
                case ReCvItemIds.EaglePlate:
                    return ItemAttribute.Key;
                case ReCvItemIds.SidePack:
                    return ItemAttribute.Special;
                case ReCvItemIds.MapRoll:
                case ReCvItemIds.HawkEmblem:
                case ReCvItemIds.QueenAntObject:
                case ReCvItemIds.KingAntObject:
                case ReCvItemIds.BiohazardCard:
                    return ItemAttribute.Key;
                case ReCvItemIds.DuraluminCaseM93RParts:
                    return ItemAttribute.Weapon;
                case ReCvItemIds.Detonator:
                case ReCvItemIds.ControlLever:
                case ReCvItemIds.GoldDragonfly:
                case ReCvItemIds.SilverKey:
                case ReCvItemIds.GoldKey:
                case ReCvItemIds.ArmyProof:
                case ReCvItemIds.NavyProof:
                case ReCvItemIds.AirForceProof:
                case ReCvItemIds.KeyWithTag:
                case ReCvItemIds.IDCard:
                    return ItemAttribute.Key;
                case ReCvItemIds.Map:
                    return ItemAttribute.Document;
                case ReCvItemIds.AirportKey:
                case ReCvItemIds.EmblemCard:
                case ReCvItemIds.SkeletonPicture:
                case ReCvItemIds.MusicBoxPlate:
                case ReCvItemIds.GoldDragonflyNoWings:
                case ReCvItemIds.Album:
                case ReCvItemIds.Halberd:
                case ReCvItemIds.Extinguisher:
                case ReCvItemIds.Briefcase:
                case ReCvItemIds.PadlockKey:
                case ReCvItemIds.TG01:
                case ReCvItemIds.SpAlloyEmblem:
                case ReCvItemIds.ValveHandle:
                case ReCvItemIds.OctaValveHandle:
                case ReCvItemIds.MachineRoomKey:
                case ReCvItemIds.MiningRoomKey:
                case ReCvItemIds.BarCodeSticker:
                case ReCvItemIds.SterileRoomKey:
                case ReCvItemIds.DoorKnob:
                case ReCvItemIds.BatteryPack:
                case ReCvItemIds.HemostaticWire:
                case ReCvItemIds.TurnTableKey:
                case ReCvItemIds.ChemStorageKey:
                case ReCvItemIds.ClementAlpha:
                case ReCvItemIds.ClementSigma:
                case ReCvItemIds.TankObject:
                case ReCvItemIds.SpAlloyEmblemUnused:
                    return ItemAttribute.Key;
                case ReCvItemIds.AlfredsMemo:
                    return ItemAttribute.Document;
                case ReCvItemIds.RustedSword:
                case ReCvItemIds.Hemostatic:
                case ReCvItemIds.SecurityCard:
                    return ItemAttribute.Key;
                case ReCvItemIds.SecurityFile:
                    return ItemAttribute.Document;
                case ReCvItemIds.AlexiasChoker:
                case ReCvItemIds.AlexiasJewel:
                case ReCvItemIds.QueenAntRelief:
                case ReCvItemIds.KingAntRelief:
                case ReCvItemIds.RedJewel:
                case ReCvItemIds.BlueJewel:
                case ReCvItemIds.Socket:
                case ReCvItemIds.SqValveHandle:
                case ReCvItemIds.Serum:
                case ReCvItemIds.EarthenwareVase:
                case ReCvItemIds.PaperWeight:
                case ReCvItemIds.SilverDragonflyNoWings:
                case ReCvItemIds.SilverDragonfly:
                case ReCvItemIds.WingObject:
                case ReCvItemIds.Crystal:
                case ReCvItemIds.GoldDragonfly1Wing:
                case ReCvItemIds.GoldDragonfly2Wings:
                case ReCvItemIds.GoldDragonfly3Wings:
                    return ItemAttribute.Key;
                case ReCvItemIds.File:
                    return ItemAttribute.Document;
                case ReCvItemIds.PlantPot:
                case ReCvItemIds.PictureB:
                    return ItemAttribute.Key;
                case ReCvItemIds.DuraluminCaseBowGunPowder:
                    return ItemAttribute.Gunpowder;
                case ReCvItemIds.DuraluminCaseMagnumRounds:
                    return ItemAttribute.Ammo;
                case ReCvItemIds.BowGunPowderUnused:
                    return ItemAttribute.Gunpowder;
                case ReCvItemIds.EnhancedHandgun:
                    return ItemAttribute.Ammo;
                case ReCvItemIds.Memo:
                case ReCvItemIds.BoardClip:
                case ReCvItemIds.Card:
                case ReCvItemIds.NewspaperClip:
                    return ItemAttribute.Document;
                case ReCvItemIds.LugerReplica:
                case ReCvItemIds.QueenAntReliefComplete:
                    return ItemAttribute.Key;
                case ReCvItemIds.FamilyPicture:
                case ReCvItemIds.FileFolders:
                    return ItemAttribute.Document;
                case ReCvItemIds.RemoteController:
                    return ItemAttribute.Key;
                case ReCvItemIds.QuestionA:
                    return ItemAttribute.Document;
                case ReCvItemIds.M1P:
                    return ItemAttribute.Weapon;
                case ReCvItemIds.CalicoBullets:
                    return ItemAttribute.Ammo;
                case ReCvItemIds.ClementMixture:
                    return ItemAttribute.Gunpowder;
                case ReCvItemIds.PlayingManual:
                case ReCvItemIds.QuestionB:
                case ReCvItemIds.QuestionC:
                case ReCvItemIds.QuestionD:
                    return ItemAttribute.Document;
                case ReCvItemIds.EmptyExtinguisher:
                    return 0;
                case ReCvItemIds.SquareSocket:
                    return ItemAttribute.Key;
                case ReCvItemIds.QuestionE:
                    return ItemAttribute.Document;
                case ReCvItemIds.CrestKeyS:
                case ReCvItemIds.CrestKeyG:
                    return ItemAttribute.Key;
                default:
                    return 0;
            }
        }

        public byte GetItemId(CommonItemKind kind)
        {
            switch (kind)
            {
                case CommonItemKind.HandgunAmmo:
                    return ReCvItemIds.HandgunBullets;
                case CommonItemKind.InkRibbon:
                    return ReCvItemIds.InkRibbon;
                case CommonItemKind.FirstAid:
                    return ReCvItemIds.FAidSpray;
                case CommonItemKind.HerbG:
                    return ReCvItemIds.GreenHerb;
                case CommonItemKind.HerbGG:
                    return ReCvItemIds.MixedHerb2Green;
                case CommonItemKind.HerbGGG:
                    return ReCvItemIds.MixedHerb3Green;
                case CommonItemKind.HerbGR:
                    return ReCvItemIds.MixedHerbRedGreen;
                case CommonItemKind.HerbGB:
                    return ReCvItemIds.MixedHerbBlueGreen;
                case CommonItemKind.HerbGGB:
                    return ReCvItemIds.MixedHerb2GreenBlue;
                case CommonItemKind.HerbGRB:
                    return ReCvItemIds.MixedHerbGreenBlueRed;
                case CommonItemKind.HerbR:
                    return ReCvItemIds.RedHerb;
                case CommonItemKind.HerbB:
                    return ReCvItemIds.BlueHerb;
                case CommonItemKind.Knife:
                    return ReCvItemIds.CombatKnife;
                default:
                    throw new NotImplementedException();
            }
        }

        public string GetItemName(byte type)
        {
            return _itemNames[type];
        }

        public double GetItemProbability(byte type)
        {
            return 0.1;
        }

        public int GetItemQuantity(RandoConfig config, byte item)
        {
            if (config.RandomDoors)
            {
                return item switch
                {
                    ReCvItemIds.EaglePlate => 3,
                    ReCvItemIds.WingObject => 4,
                    ReCvItemIds.OctaValveHandle => 2,
                    ReCvItemIds.MusicBoxPlate => 2,
                    _ => 1,
                };
            }
            else
            {
                return item switch
                {
                    ReCvItemIds.EaglePlate => 2,
                    ReCvItemIds.WingObject => 4,
                    _ => 1,
                };
            }
        }

        public byte GetItemSize(byte type)
        {
            switch (type)
            {
                case ReCvItemIds.M1P:
                case ReCvItemIds.GoldLugers:
                case ReCvItemIds.SubMachineGun:
                case ReCvItemIds.RocketLauncher:
                case ReCvItemIds.AssaultRifle:
                case ReCvItemIds.SniperRifle:
                    return 2;
                default:
                    return 1;
            }
        }

        public byte GetMaxAmmoForAmmoType(byte type)
        {
            return type switch
            {
                ReCvItemIds.RocketLauncher => 5,
                ReCvItemIds.AssaultRifle => 255,
                ReCvItemIds.SniperRifle => 7,
                ReCvItemIds.Shotgun => 7,
                ReCvItemIds.HandgunGlock17 => 18,
                ReCvItemIds.GrenadeLauncher => 6,
                ReCvItemIds.BowGun => 30,
                ReCvItemIds.CombatKnife => 0,
                ReCvItemIds.Handgun => 15,
                ReCvItemIds.CustomHandgun => 20,
                ReCvItemIds.LinearLauncher => 5,
                ReCvItemIds.HandgunBullets => 30,
                ReCvItemIds.MagnumBullets => 12,
                ReCvItemIds.ShotgunShells => 14,
                ReCvItemIds.GrenadeRounds => 12,
                ReCvItemIds.AcidRounds => 12,
                ReCvItemIds.FlameRounds => 12,
                ReCvItemIds.BowGunArrows => 60,
                ReCvItemIds.M93RPart => 18,
                ReCvItemIds.MagnumBulletsInsideCase => 12,
                ReCvItemIds.InkRibbon => 6,
                ReCvItemIds.Magnum => 6,
                ReCvItemIds.GoldLugers => 15,
                ReCvItemIds.SubMachineGun => 255,
                ReCvItemIds.BOWGasRounds => 6,
                ReCvItemIds.MGunBullets => 255,
                ReCvItemIds.RifleBullets => 14,
                ReCvItemIds.ARifleBullets => 150,
                ReCvItemIds.EnhancedHandgun => 18,
                ReCvItemIds.CalicoBullets => 200,
                _ => 0,
            };
        }

        public byte[] GetWeaponGunpowder(byte weapon)
        {
            if (weapon == ReCvItemIds.BowGun)
            {
                // Not BowGunPowderUnused: GetItem turns 130 into 35 before the
                // room script compares VAR_ITEM_ID, so scripted pickups that
                // confirm the item with cmpb would never be marked as taken.
                return new[] {
                    ReCvItemIds.BowGunPowder
                };
            }
            return new byte[0];
        }

        public WeaponKind GetWeaponKind(byte item)
        {
            switch (item)
            {
                case ReCvItemIds.Handgun:
                case ReCvItemIds.HandgunGlock17:
                    return WeaponKind.Sidearm;
                case ReCvItemIds.BowGun:
                case ReCvItemIds.AssaultRifle:
                case ReCvItemIds.SniperRifle:
                case ReCvItemIds.Shotgun:
                case ReCvItemIds.SubMachineGun:
                case ReCvItemIds.M1P:
                    return WeaponKind.Primary;
                case ReCvItemIds.LinearLauncher:
                case ReCvItemIds.GrenadeLauncher:
                case ReCvItemIds.RocketLauncher:
                case ReCvItemIds.Magnum:
                    return WeaponKind.Powerful;
                default:
                    return WeaponKind.None;
            }
        }

        public byte[] GetWeapons(Rng rng, RandoConfig config)
        {
            return new[] {
                ReCvItemIds.Handgun,
                ReCvItemIds.RocketLauncher,
                ReCvItemIds.AssaultRifle,
                ReCvItemIds.SniperRifle,
                ReCvItemIds.Shotgun,
                ReCvItemIds.HandgunGlock17,
                ReCvItemIds.GrenadeLauncher,
                ReCvItemIds.BowGun,
                ReCvItemIds.Magnum,
                ReCvItemIds.SubMachineGun,
                ReCvItemIds.M1P,
                ReCvItemIds.LinearLauncher,
            };
        }

        public string[] GetWeaponNames()
        {
            return new[]
            {
                "Handgun",
                "Rocket Launcher",
                "Assault Rifle",
                "Sniper Rifle",
                "Shotgun",
                "Handgun (Glock 17)",
                "Grenade Launcher",
                "Bowgun",
                "Magnum",
                "SMG",
                "MP100",
                "Linear Launcher",
            };
        }

        public byte? GetWeaponUpgrade(byte weapon, Rng rng, RandoConfig config)
        {
            switch (weapon)
            {
                case ReCvItemIds.Handgun:
                    return ReCvItemIds.M93RPart;
            }
            return null;
        }

        public bool HasGunPowder(RandoConfig config) => true;

        public bool HasInkRibbons(RandoConfig config) => true;

        public bool IsItemInfinite(byte type) => false;

        public bool IsOptionalItem(RandoConfig config, byte type)
        {
            return false;
        }

        public bool IsRe2ItemIdsDiscardable(byte type)
        {
            return false;
        }

        public bool IsWeaponCompatible(byte player, byte item) => true;

        private static string[] _itemNames = new[]
        {
            "None",
            "RocketLauncher",
            "AssaultRifle",
            "SniperRifle",
            "Shotgun",
            "HandgunGlock17",
            "GrenadeLauncher",
            "BowGun",
            "CombatKnife",
            "Handgun",
            "CustomHandgun",
            "LinearLauncher",
            "HandgunBullets",
            "MagnumBullets",
            "ShotgunShells",
            "GrenadeRounds",
            "AcidRounds",
            "FlameRounds",
            "BowGunArrows",
            "M93RPart",
            "FAidSpray",
            "GreenHerb",
            "RedHerb",
            "BlueHerb",
            "MixedHerb2Green",
            "MixedHerbRedGreen",
            "MixedHerbBlueGreen",
            "MixedHerb2GreenBlue",
            "MixedHerb3Green",
            "MixedHerbGreenBlueRed",
            "MagnumBulletsInsideCase",
            "InkRibbon",
            "Magnum",
            "GoldLugers",
            "SubMachineGun",
            "BowGunPowder",
            "GunPowderArrow",
            "BOWGasRounds",
            "MGunBullets",
            "GasMask",
            "RifleBullets",
            "DuraluminCaseUnused",
            "ARifleBullets",
            "AlexandersPierce",
            "AlexandersJewel",
            "AlfredsRing",
            "AlfredsJewel",
            "PrisonersDiary",
            "DirectorsMemo",
            "Instructions",
            "Lockpick",
            "GlassEye",
            "PianoRoll",
            "SteeringWheel",
            "CraneKey",
            "Lighter",
            "EaglePlate",
            "SidePack",
            "MapRoll",
            "HawkEmblem",
            "QueenAntObject",
            "KingAntObject",
            "BiohazardCard",
            "DuraluminCaseM93RParts",
            "Detonator",
            "ControlLever",
            "GoldDragonfly",
            "SilverKey",
            "GoldKey",
            "ArmyProof",
            "NavyProof",
            "AirForceProof",
            "KeyWithTag",
            "IDCard",
            "Map",
            "AirportKey",
            "EmblemCard",
            "SkeletonPicture",
            "MusicBoxPlate",
            "GoldDragonflyNoWings",
            "Album",
            "Halberd",
            "Extinguisher",
            "Briefcase",
            "PadlockKey",
            "TG01",
            "SpAlloyEmblem",
            "ValveHandle",
            "OctaValveHandle",
            "MachineRoomKey",
            "MiningRoomKey",
            "BarCodeSticker",
            "SterileRoomKey",
            "DoorKnob",
            "BatteryPack",
            "HemostaticWire",
            "TurnTableKey",
            "ChemStorageKey",
            "ClementAlpha",
            "ClementSigma",
            "TankObject",
            "SpAlloyEmblemUnused",
            "AlfredsMemo",
            "RustedSword",
            "Hemostatic",
            "SecurityCard",
            "SecurityFile",
            "AlexiasChoker",
            "AlexiasJewel",
            "QueenAntRelief",
            "KingAntRelief",
            "RedJewel",
            "BlueJewel",
            "Socket",
            "SqValveHandle",
            "Serum",
            "EarthenwareVase",
            "PaperWeight",
            "SilverDragonflyNoWings",
            "SilverDragonfly",
            "WingObject",
            "Crystal",
            "GoldDragonfly1Wing",
            "GoldDragonfly2Wings",
            "GoldDragonfly3Wings",
            "File",
            "PlantPot",
            "PictureB",
            "DuraluminCaseBowGunPowder",
            "DuraluminCaseMagnumRounds",
            "BowGunPowderUnused",
            "EnhancedHandgun",
            "Memo",
            "BoardClip",
            "Card",
            "NewspaperClip",
            "LugerReplica",
            "QueenAntReliefComplete",
            "FamilyPicture",
            "FileFolders",
            "RemoteController",
            "QuestionA",
            "M1P",
            "CalicoBullets",
            "ClementMixture",
            "PlayingManual",
            "QuestionB",
            "QuestionC",
            "QuestionD",
            "EmptyExtinguisher",
            "SquareSocket",
            "QuestionE",
            "CrestKeyS",
            "CrestKeyG"
        };
    }
}
