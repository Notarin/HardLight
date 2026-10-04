using Content.Server._HL.Whisp;

namespace Content.Server._HL.StationEvents.Components;

/// <summary>
/// Component for the whisp spawn station event rule.
/// Selects an eligible player on the station map (preferring the most isolated/distant crew)
/// and triggers a whisp haunt.
/// </summary>
[RegisterComponent]
public sealed partial class WhispSpawnRuleComponent : Component
{
    /// <summary>
    /// The player entity selected to be haunted and stalked.
    /// </summary>
    [DataField]
    public EntityUid? HauntedPlayer;

    /// <summary>
    /// Initial stalking distance when the whisp spawns.
    /// </summary>
    [DataField]
    public float WhispDistance = 20f;

    /// <summary>
    /// Speed at which the whisp closes distance to the target.
    /// </summary>
    [DataField]
    public float WhispSpeed = 0.5f;

    /// <summary>
    /// Optional custom haunt steps. If null, use the default sequence defined on <see cref="WhispHauntComponent"/>.
    /// </summary>
    [DataField]
    public List<WhispHauntStep>? HauntSteps;
}
