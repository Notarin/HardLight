using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Mind.Filters;

/// <summary>
/// A mind pool that can find minds to use for objectives etc.
/// Further filtered by IMindFilter.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public partial interface IMindPool
{
    /// <summary>
    /// Add minds for this pool to a hashset.
    /// The hashset gets reused and is cleared before this is called.
    /// </summary>
    void FindMinds(HashSet<Entity<MindComponent>> minds, EntityUid? exclude, IEntityManager entMan, SharedMindSystem mindSys);
}
