using Content.Shared.Popups;
using Robust.Shared.Audio;

namespace Content.Server._HL.Whisp;

/// <summary>
/// Attached to an entity undergoing a whisp haunt before a whisp spawns to stalk them.
/// </summary>
[RegisterComponent, Access(typeof(WhispHauntSystem))]
public sealed partial class WhispHauntComponent : Component
{
    /// <summary>
    /// Index of the current active step in <see cref="Steps"/>.
    /// </summary>
    [DataField]
    public int Step;

    /// <summary>
    /// Elapsed seconds within the current step.
    /// </summary>
    [DataField]
    public float Elapsed;

    /// <summary>
    /// Initial stalking distance when the whisp spawns at the conclusion of the haunt.
    /// </summary>
    [DataField]
    public float WhispDistance = 20f;

    /// <summary>
    /// Closing speed of the whisp spawned at the conclusion of the haunt.
    /// </summary>
    [DataField]
    public float WhispSpeed = 0.5f;

    /// <summary>
    /// Whether the haunt successfully finished and spawned its whisp.
    /// </summary>
    [DataField]
    public bool Finished;

    /// <summary>
    /// Ordered list of suspenseful steps executed during the countdown.
    /// </summary>
    [DataField]
    public List<WhispHauntStep> Steps =
    [
        new() { Delay = 30f, Message = "whisp-haunt-popup-1", PopupType = PopupType.LargeCaution },
        new() { Delay = 30f, Message = "whisp-haunt-popup-2", PopupType = PopupType.LargeCaution },
        new() { Delay = 10f, Message = "whisp-haunt-popup-3", PopupType = PopupType.LargeCaution },
    ];
}

/// <summary>
/// Defines an individual stage of a whisp haunt.
/// </summary>
[DataDefinition]
public sealed partial class WhispHauntStep
{
    /// <summary>
    /// Duration in seconds to wait before advancing.
    /// </summary>
    [DataField]
    public float Delay = 30f;

    /// <summary>
    /// The message displayed to the victim. (LOC)
    /// </summary>
    [DataField]
    public LocId? Message;

    /// <summary>
    /// Visual popup style for the message.
    /// </summary>
    [DataField]
    public PopupType PopupType = PopupType.LargeCaution;

    /// <summary>
    /// Whether to also echo the message into the victim's local chat channel.
    /// </summary>
    [DataField]
    public bool SendChat = true;

    /// <summary>
    /// Optional audio played directed to the victim.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound;
}
