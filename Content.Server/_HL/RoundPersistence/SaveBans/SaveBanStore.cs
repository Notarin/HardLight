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
    /// This is the type for the actual representation of items that are officially restricted from saving in some form or another.
    /// Also contains some factory methods to make adding new banned items simplified.
    /// </summary>
    /// <param name="BannedFlag"><see cref="Content.Server._HL.RoundPersistence.SaveBans.SaveBanStore.SaveBanFlag"/></param>
    /// <param name="Reason">A serialized formal explanation of why we cannot allow players to save this item.</param>
    public sealed record SaveBan(
        SaveBanFlag BannedFlag,
        string Reason)
    {
        public static SaveBan Entity(string prototype, string reason) => new(new SaveBanFlag.SaveBanFlagByEntity(prototype), reason);
        public static SaveBan Component(string prototype, string reason) => new(new SaveBanFlag.SaveBanFlagByComponent(prototype), reason);
    }
}
