using Content.Shared.Abilities.Psionics;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared.Nyanotrasen.Abilities.Psionics;

/// <summary>
/// Base class for psionic power function implementations
/// </summary>
public abstract partial class PsionicPowerFunction : Robust.Shared.Serialization.ISerializationHooks
{
    /// <summary>
    /// Called when a psionic power is added to an entity
    /// </summary>
    public abstract void OnAddPsionic(
        EntityUid uid,
        IComponentFactory factory,
        IEntityManager entityManager,
        ISerializationManager serializationManager,
        ISharedPlayerManager playerManager,
        ILocalizationManager loc,
        PsionicComponent psionicComponent,
        PsionicPowerPrototype proto
    );

    /// <summary>
    /// Called when a psionic power is removed from an entity
    /// </summary>
    public virtual void OnRemovePsionic(
        EntityUid uid,
        IComponentFactory factory,
        IEntityManager entityManager,
        ISerializationManager serializationManager,
        ISharedPlayerManager playerManager,
        ILocalizationManager loc,
        PsionicComponent psionicComponent,
        PsionicPowerPrototype proto
    )
    {
        // Base implementation - do nothing
    }

    void Robust.Shared.Serialization.ISerializationHooks.AfterDeserialization()
    {
        // Base implementation - do nothing
    }
}
