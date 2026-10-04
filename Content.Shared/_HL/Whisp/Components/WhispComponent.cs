using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._HL.Whisp.Components;

/// <summary>
/// Attached to a whisp entity. Stalks a target at a distance with a fixed rate of decrease.
/// </summary>
/// <remarks>
/// If you are seeing this before you have heard about this, this is a secret feature.
/// The fun is the surprise. Do not ruin it.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WhispComponent : Component
{
    /// <summary>
    /// The target entity the whisp is stalking.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    /// <summary>
    /// Current distance in meters between the whisp and the target.
    /// Decreases every tick until reaching ReachDistance.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CurrentDistance = 20f;

    /// <summary>
    /// How fast the whisp approaches the target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ClosingSpeed = 0.5f;

    /// <summary>
    /// The distance at which the whisp triggers the reached event and despawns.
    /// </summary>
    /// <remarks>
    /// In actual practice can vary due to networking limitations and optimizations in place to handle them.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public float ReachDistance = 0.35f;

    /// <summary>
    /// The last known unit direction vector pointing from target to whisp.
    /// This is used to determine what direction we should move the whisp to have it stay at its desired distance.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 LastDirection = Vector2.UnitY;

    /// <summary>
    /// Client-side predicted world position of the whisp to ensure smooth interpolation
    /// free from network jitter or server transform lag.
    /// This is client-side only, and ofc not networked.
    /// </summary>
    /// <remarks>
    /// A lot is done to ensure the client predicts the whisps position as betst as possible.
    /// </remarks>
    [NonSerialized]
    public Vector2? PredictedPosition;
}
