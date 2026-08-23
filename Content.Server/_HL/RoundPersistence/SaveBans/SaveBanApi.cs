using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Server._HL.RoundPersistence.SaveBans;

/// <summary>
/// This class is the API or interface for checking what is save-banned and what isn't.
/// Generally speaking a null response indicates clear status, or not banned.
/// </summary>
public sealed class SaveBanApi : EntitySystem
{
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    /// <summary>
    /// The actual save-banned list itself.
    /// </summary>
    public IReadOnlyList<SaveBanStore.SaveBan> Bans = null!;

    public override void Initialize()
    {
        base.Initialize();
        Bans = _prototypeManager.EnumeratePrototypes<SaveBanPrototype>()
            .SelectMany(ban =>
                ban.Prototypes.Select(prototype =>
                        SaveBanStore.SaveBan.Entity(prototype,
                            ban.Reason,
                            new SaveBanStore.SaveRestrictionStrictness.TotalBan()))
                    .Concat(
                        ban.Components.Select(component =>
                            SaveBanStore.SaveBan.Component(component,
                                ban.Reason,
                                new SaveBanStore.SaveRestrictionStrictness.TotalBan()))
                    ))
            .ToList();
    }

    /// <summary>
    /// This is the type that is returned when querying an items ban status.
    /// It includes the item uid itself, alongside any bans found, and relevant info.
    /// </summary>
    public abstract record SaveBanResult(EntityUid EntityUid)
    {
        /// <summary>
        /// This response variant indicates the item itself has a ban or restriction.
        /// </summary>
        public sealed record IsSaveRestricted(EntityUid EntityUid, SaveBanStore.SaveBan Ban) : SaveBanResult(EntityUid);

        /// <summary>
        /// This response variant indicates that while this item does *not* have an outstanding ban or restriction,
        /// it contains items that do.
        /// </summary>
        public sealed record ContainsSaveRestricted(EntityUid EntityUid, IReadOnlyList<SaveBanResult> Restrictions)
            : SaveBanResult(EntityUid);
    }

    /// <summary>
    /// This is the primary checking method. Given one entity, it checks it, and any children that it may contain.
    /// </summary>
    public SaveBanResult? CheckForRestrictions(EntityUid entityUid)
    {
        var entityPrototype = Prototype(entityUid)?.ID;

        if (entityPrototype is null)
            return null;

        var ban = Bans.FirstOrDefault(ban =>
        {
            return ban.BannedFlag switch
            {
                SaveBanStore.SaveBanFlag.SaveBanFlagByEntity entityFlag =>
                    entityFlag.Prototype == entityPrototype,
                SaveBanStore.SaveBanFlag.SaveBanFlagByComponent componentFlag =>
                    HasComp(entityUid, _componentFactory.GetRegistration(componentFlag.Name).Type),
                _ => throw new ArgumentOutOfRangeException(ban.BannedFlag.ToString()),
            };
        });

        if (ban is not null)
            return new SaveBanResult.IsSaveRestricted(entityUid, ban);

        var restrictions = new List<SaveBanResult>();

        var childEnumerator = Transform(entityUid).ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            var result = CheckForRestrictions(child);

            if (result is not null)
                restrictions.Add(result);
        }

        return restrictions.Count > 0
            ? new SaveBanResult.ContainsSaveRestricted(
                entityUid,
                restrictions)
            : null;
    }

    /// <summary>
    /// Helper method. To take the recursive ContainsSaveRestricted, and flatten it down into an enumerable list of IsSaveRestricteds
    /// </summary>
    public static IEnumerable<SaveBanResult.IsSaveRestricted> FlattenRestrictions(SaveBanResult result)
    {
        switch (result)
        {
            case SaveBanResult.IsSaveRestricted restricted:
                yield return restricted;
                yield break;

            case SaveBanResult.ContainsSaveRestricted contains:
                foreach (var child in contains.Restrictions)
                {
                    foreach (var flattened in FlattenRestrictions(child))
                    {
                        yield return flattened;
                    }
                }
                yield break;

            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }
    }
}
