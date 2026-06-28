using Content.Shared.Abilities.Psionics;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Psionics.Events;

[Serializable, NetSerializable]
public sealed partial class PsionicRegenerationDoAfterEvent : DoAfterEvent
{
    [DataField("startedAt", required: true)]
    public TimeSpan StartedAt;

    public PsionicRegenerationDoAfterEvent(TimeSpan startedAt)
    {
        StartedAt = startedAt;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class GlimmerWispDrainDoAfterEvent : SimpleDoAfterEvent { }

[Serializable, NetSerializable]
public sealed partial class HealingWordDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public TimeSpan StartedAt;

    public HealingWordDoAfterEvent(TimeSpan startedAt)
    {
        StartedAt = startedAt;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class PsionicHealOtherDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public TimeSpan StartedAt;

    [DataField]
    public DamageSpecifier? HealingAmount = default!;

    [DataField]
    public float? RotReduction;

    [DataField]
    public bool DoRevive;

    [DataField]
    public float ModifiedAmplification = 1.0f;

    [DataField]
    public float ModifiedDampening = 1.0f;

    public PsionicHealOtherDoAfterEvent(TimeSpan startedAt)
    {
        StartedAt = startedAt;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class AssayDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public TimeSpan StartedAt;

    [DataField]
    public int FontSize = 12;

    [DataField]
    public string FontColor = "#8A00C2";

    private AssayDoAfterEvent() { }

    public AssayDoAfterEvent(TimeSpan startedAt, int fontSize, string fontColor)
    {
        StartedAt = startedAt;
        FontSize = fontSize;
        FontColor = fontColor;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class LifeDrainDoAfterEvent : SimpleDoAfterEvent { }
