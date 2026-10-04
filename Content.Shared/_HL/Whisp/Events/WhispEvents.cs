using Content.Shared._HL.Whisp.Components;

namespace Content.Shared._HL.Whisp.Events;

/// <summary>
/// Fired when a whisp reaches its stalked target, right before the whisp despawns.
/// Raised directed on the target and broadcast globally.
/// It is *not* raised on the whisp, as it is despawning at the same instant.
/// </summary>
public sealed class WhispReachedTargetEvent(EntityUid whisp, EntityUid target) : EntityEventArgs
{
    /// <summary>
    /// See <see cref="WhispComponent" />.<br/>
    /// The whisp that reached that caught their target.
    /// </summary>
    /// TODO: Make something actually happen when it gets ya.
    public EntityUid Whisp { get; } = whisp;
    /// <summary>
    /// The target that the whisp caught.
    /// </summary>
    /// TODO: Make something actually happen when it gets ya.
    public EntityUid Target { get; } = target;
}

/// <summary>
/// Event to request spawning a whisp tracking a target.
/// Can be raised, directed, or broadcast to trigger a whisp hunt.
/// </summary>
/// TODO: Make something actually happen when it gets ya.
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
