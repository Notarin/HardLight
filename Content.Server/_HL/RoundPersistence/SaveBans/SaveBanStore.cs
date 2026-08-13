using static Content.Server._HL.RoundPersistence.SaveBans.SaveBanStore.SaveBan;
using static Content.Server._HL.RoundPersistence.SaveBans.SaveBanStore.SaveRestrictionStrictness;

namespace Content.Server._HL.RoundPersistence.SaveBans;

/// <summary>
/// This is the class which holds a collection of all the items in the game with some form of save ban or save restriction.
/// </summary>
public static class SaveBanStore
{
    /// <summary>
    /// Flags are the specific identities of what are banned. It's inheritors each define a specific type of identifier.
    /// </summary>
    public abstract record SaveBanFlag
    {
        /// <summary>
        /// This flag type refers to specifically prototyped entities that are save-restricted, identified by their prototype name.
        /// </summary>
        /// <param name="Prototype">The name of the prototype for the entity.</param>
        public sealed record SaveBanFlagByEntity(string Prototype) : SaveBanFlag;
        /// <summary>
        /// This flag type refers to the items that are considered save-restricted via their component.
        /// </summary>
        /// <param name="Name">The prototype name for the component</param>
        public sealed record SaveBanFlagByComponent(string Name) : SaveBanFlag;
    }

    /// <summary>
    /// This type represents the strictness of the ban on said item.
    /// The strictness represents what course of action will be taken on an item should someone try to save it.
    /// </summary>
    public abstract record SaveRestrictionStrictness
    {
        /// <summary>
        /// The strictest ban possible. If you try to save this item, it will be deleted, and unrecoverable.
        /// </summary>
        public sealed record TotalBan : SaveRestrictionStrictness;
        /// <summary>
        /// A lighter form of restriction.
        /// When an item with this strictness is saved, during the check, it's associated handler will be executed on the item.
        /// This will usually mean wiping or reverting some data stored on said item.
        /// </summary>
        public abstract record PartialBan : SaveRestrictionStrictness
        {
            /// <summary>
            /// This handler will be invoked when the item associated it saved.
            /// Frequently used to strip data not intended to be saved.
            /// </summary>
            /// <param name="entityUid">The uid of the entity this handler was invoked for.</param>
            public abstract void Handle(EntityUid entityUid);
        }
    }

    /// <summary>
    /// This is the type for the actual representation of items that are officially restricted from saving in some form or another.
    /// Also contains some factory methods to make adding new banned items simplified.
    /// </summary>
    /// <param name="BannedFlag"><see cref="Content.Server._HL.RoundPersistence.SaveBans.SaveBanStore.SaveBanFlag"/></param>
    /// <param name="Reason">A serialized formal explanation of why we cannot allow players to save this item.</param>
    /// <param name="Strictness"><see cref="Content.Server._HL.RoundPersistence.SaveBans.SaveBanStore.SaveRestrictionStrictness"/></param>
    public sealed record SaveBan(
        SaveBanFlag BannedFlag,
        string Reason,
        SaveRestrictionStrictness Strictness)
    {
        public static SaveBan Entity(string prototype, string reason, SaveRestrictionStrictness strictness) =>
            new(new SaveBanFlag.SaveBanFlagByEntity(prototype), reason, strictness);
        public static SaveBan Component(string prototype, string reason, SaveRestrictionStrictness strictness) =>
            new(new SaveBanFlag.SaveBanFlagByComponent(prototype), reason, strictness);
    }
    /// <summary>
    /// The actual save-banned list itself. This is where the configuration takes place.
    /// </summary>
    public static IReadOnlyList<SaveBan> Bans { get; } =
    [
        Entity("DeathRattleImplanterColcomm", "At risk of game engine abuse.", new TotalBan()),
        Entity("RadioImplanterColcomm", "At risk of game engine abuse.", new TotalBan()),
        Entity("UplinkImplanter", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponPistolViper", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponRevolverPythonAP", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponPistolCobra", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponSniperMosin", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponSniperHristov", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponSubMachineGunC20r", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponShotgunBulldog", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponLightMachineGunL6", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponSubMachineGunAtreides", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponLauncherChinaLake", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponRifleM90GrenadeLauncher", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponFlamethrowerFG", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SEnergySword", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SEnergyDagger", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SClothingHandsGlovesNorthStar", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SClothingHandsKnuckleDustersSyndicate", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SExGrenade", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SSyndieMiniBomb", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SC4", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SPenExplodingBox", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SPenExploding", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SClusterGrenade", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SGrenadeShrapnel", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SGrenadeIncendiary", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SClothingOuterHardsuitSyndie", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SClothingOuterHardsuitJuggernaut", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SClothingOuterHardsuitSyndieElite", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SClothingOuterHardsuitSyndieCommander", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ToolboxElectricalTurretFilled", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("BriefcaseSyndieSniperBundleFilled", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingBackpackDuffelSyndicateFilledSMG", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingBackpackDuffelSyndicateFilledShotgun", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingBackpackDuffelSyndicateFilledGrenadeLauncher", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingBackpackDuffelSyndicateFilledLMG", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SingularityGrenade", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("WhiteholeGrenade", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("MobGrenadePenguin", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingBackpackDuffelSyndicateC4tBundle", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingBeltMilitaryWebbingGrenadeFilled", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SyndicateBomb", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ElectricalDisruptionKit", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingBackpackDuffelSyndicateAmmoFilled", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("HypopenBox", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("HypoDartBox", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ChemicalSynthesisKit", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingBackpackDuffelZombieBundle", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ChemistryBottleNocturine", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingBackpackDuffelSyndicateMedicalBundleFilled", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("AgentIDCard", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ChameleonProjector", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("BoxEncryptionKeySyndie", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("EncryptionKeyBinarySyndicate", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("EncryptionKeySyndicate", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("NukeDiskFake", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("BriefcaseSyndieLobbyingBundleFilled", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SyndicateBombFake", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("AccessBreaker", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("Emag", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("BorgModuleSyndicateWeapon", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("BorgModuleMartyr", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("PowerSink", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("AntimovCircuitBoard", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("CrateSyndicateSurplusBundle", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("CrateSyndicateSuperSurplusBundle", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SingularityBeacon", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("CameraBug", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("BoxHoloparasite", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ReinforcementRadioSyndicate", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ReinforcementRadioSyndicateNukeops", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ReinforcementRadioSyndicateCyborgAssault", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ReinforcementRadioSyndicateAncestor", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ReinforcementRadioSyndicateAncestorNukeops", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ReinforcementRadioSyndicateSyndiCat", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("StorageImplanter", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("FreedomImplanter", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ScramImplanter", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("DnaScramblerImplanter", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("EmpImplanter", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("RadioImplanter", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("MicroBombImplanter", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("MacroBombImplanter", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("DeathAcidifierImplanter", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("BoxDeathRattleImplants", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("FakeMindShieldImplanter", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingShoesChameleonNoSlips", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingBackpackSyndicateRaidBundle", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingHandsGlovesBoxingRigged", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("BibleNecronomicon", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("HolyHandGrenade", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("TrashBananaPeelExplosiveUnarmed", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClusterBananaPeel", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("BoxHoloclown", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("HotPotato", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("WeaponPistolCHIMPUpgradeKit", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("WetFloorSignMineExplosive", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SawAdvanced", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("EnergyDomeGeneratorPersonalSyndie", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingOuterArmorRaid", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("ClothingHandsGlovesConducting", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("BorgModuleL6C", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponLightMachineGunL6C", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("BorgModuleEsword", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("CyborgEnergySwordDouble", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("BorgModuleOperative", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("SWeaponPistolEchis", "Antag equipment. Destructive to the intended game loop.", new TotalBan()),
        Entity("CommsComputerCircuitboard", "Liable to be destructive to game enjoyment server-wide.", new TotalBan()),

        Entity("ComputerDNAScanner", "Not intended for removal from station.", new TotalBan()),
        Entity("ComputerExpeditionDiskPrinter", "Not intended for removal from station.", new TotalBan()),
        Entity("ComputerFundingAllocation", "Not intended for removal from station.", new TotalBan()),
        Entity("ComputerPsionicsRecords", "Not intended for removal from station.", new TotalBan()),
        Entity("ComputerRoboticsControl", "Not intended for removal from station.", new TotalBan()),
        Entity("ComputerShuttleRecords", "Not intended for removal from station.", new TotalBan()),
        Entity("DnaScannerConsoleComputerCircuitboard", "Not intended for removal from station.", new TotalBan()),
        Entity("IDComputerCircuitboard", "Not intended for removal from station.", new TotalBan()),
        Entity("StationAiUploadComputer", "Not intended for removal from station.", new TotalBan()),
        Entity("MachineParadoxGenerator", "Not intended for removal from station.", new TotalBan()),
        Entity("ParadoxGeneratorMachineCircuitboard", "Not intended for removal from station.", new TotalBan()),

        Entity("DEBUGVendingMachineAmmoBoxes", "Explicitly unobtainable.", new TotalBan()),
        Entity("DEBUGVendingMachineMagazines", "Explicitly unobtainable.", new TotalBan()),
        Entity("DEBUGVendingMachineRangedWeapons", "Explicitly unobtainable.", new TotalBan()),

        Entity("VendingMachineAmmoPOI", "Not intended for removal from station.", new TotalBan()),
        Entity("VendingMachineAstroVendPOI", "Not intended for removal from station.", new TotalBan()),
        Entity("VendingMachineBoozePOI", "Not intended for removal from station.", new TotalBan()),
        Entity("VendingMachineBountyVendPOI", "Not intended for removal from station.", new TotalBan()),
        Entity("VendingMachineCigsPOI", "Not intended for removal from station.", new TotalBan()),
        Entity("VendingMachineEngivendPOI", "Not intended for removal from station.", new TotalBan()),
        Entity("VendingMachineExpeditionaryFlatpackVend", "Not intended for removal from docks.", new TotalBan()),
        Entity("VendingMachineFlatpackVend", "Not intended for removal from docks.", new TotalBan()),
        Entity("VendingMachineFuelVend", "Not intended for removal from docks.", new TotalBan()),
        Entity("VendingMachineGamesPOI", "Not intended for removal from station.", new TotalBan()),
        Entity("LessLethalVendingMachinePOI", "Not intended for removal from station.", new TotalBan()),
        Entity("VendingMachineMediDrobePOI", "Not intended for removal from station.", new TotalBan()),
        Entity("VendingMachineMercVend", "Not intended for removal from docks.", new TotalBan()),
        Entity("VendingMachinePickNPackPOI", "Not intended for removal from station.", new TotalBan()),
        Entity("VendingMachinePottedPlantVendPOI", "Not intended for removal from station.", new TotalBan()),
        Entity("VendingMachineSalvagePOI", "Not intended for removal from station.", new TotalBan()),
        Entity("VendingMachineSyndieContraband", "Balance breaking.", new TotalBan()),
        Entity("VendingMachineTankDispenserEVAPOI", "Not intended for removal from station.", new TotalBan()),
        Entity("VendingMachineVendomatPOI", "Not intended for removal from station.", new TotalBan()),
        Entity("VendingMachineYouToolPOI", "Not intended for removal from station.", new TotalBan()),

        Entity("WeaponLauncherChinaLake", "Balance breaking.", new TotalBan()),
        Entity("ClothingOuterHardsuitCybersunStealth", "Balance breaking.", new TotalBan()),
        Entity("ThievingGloves", "Balance breaking", new TotalBan()),
        Entity("CQCManual", "Balance breaking.", new TotalBan()),
        Entity("ClothingOuterHardsuitNfsdExperimental", "Balance breaking.", new TotalBan()),

        Component("CommunicationsConsole", "Liable to be destructive to game enjoyment server-wide.", new TotalBan()),
        Component("ContrabandPalletConsole", "Balance breaking.", new TotalBan()),
        Component("CriminalRecordsConsole", "Not intended for removal from station.", new TotalBan()),
        Component("DnaSequenceInjector", "Balance breaking.", new TotalBan()),
        Component("DoorRemote", "Not intended for removal from station.", new TotalBan()),
        Component("EmergencyShuttleConsole", "Not intended for removal from station.", new TotalBan()),
        Component("GeneralStationRecordConsole", "Not intended for removal from station.", new TotalBan()),
        Component("GeneticAnalyzer", "Not intended for removal from station.", new TotalBan()),
        Component("IdCard", "Balance breaking.", new TotalBan()),
        Component("IdCardConsole", "Not intended for removal from station.", new TotalBan()),
        Component("MarketConsole", "Not intended for removal from station.", new TotalBan()),
        Component("NFCargoOrderConsole", "Not intended for removal from station.", new TotalBan()),
        Component("Pda", "Liable for abuse in certain contexts.", new TotalBan()),
        Component("ShipyardConsole", "Not intended for removal from station or docks.", new TotalBan()),
        Component("Store", "Balance breaking; Infinite resources.", new TotalBan()),
        Component("Emag", "Balance breaking.", new TotalBan()),
        Component("MagicMirror", "unable to be saved.", new TotalBan()),
    ];
}
