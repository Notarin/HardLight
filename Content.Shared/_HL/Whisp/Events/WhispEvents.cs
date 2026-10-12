using Content.Shared._HL.Whisp.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._HL.Whisp.Events;

/// <summary>
/// Fired when a whisp reaches its stalked target, right before the whisp despawns.
/// Network event raised by the server to inform the client that a whisp caught the player.
/// Triggers the screech and overlay effects.
/// </summary>
[Serializable, NetSerializable]
public sealed class WhispReachedTargetEvent(NetEntity whisp, NetEntity target) : EntityEventArgs
{
    /// <summary>
    /// The whisp entity that caught the target.
    /// </summary>
    public NetEntity Whisp { get; } = whisp;

    /// <summary>
    /// The target player entity that was caught.
    /// </summary>
    public NetEntity Target { get; } = target;
}

/// <summary>
/// Event to request spawning a whisp tracking a target.
/// Can be raised, directed, or broadcast to trigger a whisp hunt.
/// </summary>
public sealed class SpawnWhispEvent(EntityUid target, float distance = 20f, float speed = 0.5f) : EntityEventArgs
{
    /// <summary>
    /// The target that the whisp is tracking.
    /// </summary>
    public EntityUid Target { get; } = target;

    /// <inheritdoc cref="WhispComponent.CurrentDistance" />
    public float Distance { get; } = distance;

    /// <inheritdoc cref="WhispComponent.ClosingSpeed" />
    public float Speed { get; } = speed;
}
