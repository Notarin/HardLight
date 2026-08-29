namespace Content.Server._HL.RoundPersistence.SaveBans;

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
    }

    /// <summary>
    /// This type represents the strictness of the ban on the said item.
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
        /// When an item with this strictness is saved, during the check, its associated handler will be executed on the item.
        /// This will usually mean wiping or reverting some data stored on the said item.
        /// </summary>
        /// <param name="Handle">A function that takes an entity manager, and the entity uid, and performs an arbitrary action.</param>
        public abstract record PartialBan(Action<EntityManager, EntityUid> Handle) : SaveRestrictionStrictness;
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
}
