using Content.Shared.Atmos;

namespace Content.Shared._HL.RoundPersistence.SaveBans;

/// <summary>
/// This is the class that holds a collection of all the items in the game with some form of save ban or save restriction.
/// </summary>
public static partial class SaveBanStore
{
    /// <summary>
    /// Flags are the specific identities of what are banned. Its inheritors each define a specific type of identifier.
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

        /// <summary>
        /// A subset of flags targeting seeds specifically.
        /// </summary>
        /// <remarks>
        /// Does not restrict or ban the flagged offender from the seed itself!
        /// Instead, it bans the seed from being saved.
        /// </remarks>
        public abstract record SaveBanSeedFlag : SaveBanFlag
        {
            /// <summary>
            /// This flag type refers to reagents in seeds which are save-restricted.
            /// </summary>
            /// <param name="ReagentName">The name of the reagent to ban from savable seeds.</param>
            public sealed record SaveBanSeedFlagByReagent(string ReagentName) : SaveBanSeedFlag;
            /// <summary>
            /// This flag type refers to gasses in seeds which are save-restricted.
            /// </summary>
            /// <param name="ProducedGas">The gas to ban from savable seeds.</param>
            public sealed record SaveBanSeedFlagByGasProduction(Gas ProducedGas) : SaveBanSeedFlag;
        }
    }

    /// <summary>
    /// This type represents the strictness of the ban on the said item.
    /// The strictness represents what course of action will be taken on an item should someone try to save it.
    /// </summary>
    public abstract partial class SaveRestrictionStrictness
    {
        /// <summary>
        /// The strictest ban possible. If you try to save this item, it will be deleted, and unrecoverable.
        /// </summary>
        [DataDefinition]
        public sealed partial class TotalBan : SaveRestrictionStrictness;
        /// <summary>
        /// A lighter form of restriction.
        /// When an item with this strictness is saved, during the check, the offender will be stripped off the item.
        /// This usually means removing a component from the item.
        /// Currently, this is incompatible with <see cref="SaveBanFlag.SaveBanFlagByEntity"/> bans.
        /// TODO: Refactor saveban system to disallow the potentially bad state of StripBanned prototypes. (TypeEnforcedValidSavebanFlagStrictness)
        /// </summary>
        [DataDefinition]
        public abstract partial class StripBan : SaveRestrictionStrictness;
    }

    /// <summary>
    /// This is the type for the actual representation of items that are officially restricted from saving in some form or another.
    /// Also contains some factory methods to make adding new banned items simplified.
    /// </summary>
    /// <param name="BannedFlag"><see cref="SaveBanFlag"/></param>
    /// <param name="Reason">A serialized formal explanation of why we cannot allow players to save this item.</param>
    /// <param name="Strictness"><see cref="SaveRestrictionStrictness"/></param>
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
}
