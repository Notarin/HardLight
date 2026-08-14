using System.Numerics;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._HL.PoolToy;

/// <summary>
/// An inflatable body that springs a leak when cut or punctured, bleeding air until the breach is patched up.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PoolToyInflationComponent : Component
{
    /// <summary>
    /// Damage types that can open a breach.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<DamageTypePrototype>> PunctureDamageTypes = new() { "Slash", "Piercing" };

    /// <summary>
    /// How much puncturing damage a single hit needs to deal to open a breach.
    /// </summary>
    [DataField]
    public FixedPoint2 PunctureThreshold = FixedPoint2.New(1);

    /// <summary>
    /// Damage type the escaping air is dealt as.
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> AirlossDamageType = "Asphyxiation";

    /// <summary>
    /// Airloss dealt per second while a breach is open. This has to outpace the respirator, which heals
    /// asphyxiation while the entity is still breathing.
    /// </summary>
    [DataField]
    public FixedPoint2 AirlossPerSecond = FixedPoint2.New(2);

    /// <summary>
    /// How long sealing a breach takes.
    /// </summary>
    [DataField]
    public TimeSpan SealDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan DeflateInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextDeflate;

    /// <summary>
    /// Whether air is currently escaping.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Breached;

    /// <summary>
    /// Sprite scale at full health.
    /// </summary>
    [DataField]
    public Vector2 InflatedScale = Vector2.One;

    /// <summary>
    /// Sprite scale reached as damage approaches the crit threshold.
    /// </summary>
    [DataField]
    public Vector2 DeflatedScale = new(0.85f, 0.55f);

    /// <summary>
    /// Sprite scale once out of air, i.e. in crit. Squashed along the other axis, since a crit body is lying
    /// down and its sprite is turned on its side.
    /// </summary>
    [DataField]
    public Vector2 FlatScale = new(0.7f, 1.1f);

    /// <summary>
    /// How long topping an inflatable body up from a gas tank takes.
    /// </summary>
    [DataField]
    public TimeSpan RefillDelay = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Airloss healed per mole taken out of the tank. A regular tank holds a couple of moles, which is
    /// enough to clear all of it in one go; emergency tanks only get part of the way.
    /// </summary>
    [DataField]
    public FixedPoint2 AirlossHealedPerMole = FixedPoint2.New(100);

    /// <summary>
    /// A tank with less than this left in it is too empty to bother with.
    /// </summary>
    [DataField]
    public float MinRefillMoles = 0.02f;

    /// <summary>
    /// How often the leaking entity gets reminded that it is losing air.
    /// </summary>
    [DataField]
    public TimeSpan WarningInterval = TimeSpan.FromSeconds(8);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextWarning;

    [DataField]
    public LocId BreachPopup = "pooltoy-breached";

    [DataField]
    public LocId BreachPopupOthers = "pooltoy-breached-others";

    [DataField]
    public LocId DeflatingPopup = "pooltoy-deflating";

    [DataField]
    public LocId DeflatingPopupOthers = "pooltoy-deflating-others";

    [DataField]
    public LocId FlatPopup = "pooltoy-flat";

    [DataField]
    public LocId FlatPopupOthers = "pooltoy-flat-others";

    [DataField]
    public LocId SealPopup = "pooltoy-sealed";

    [DataField]
    public LocId SealPopupOthers = "pooltoy-sealed-others";

    [DataField]
    public LocId SealingPopupOthers = "pooltoy-sealing-others";

    [DataField]
    public LocId HealedPopupOthers = "pooltoy-healed-others";

    [DataField]
    public LocId RefillPopup = "pooltoy-refilled";

    [DataField]
    public LocId RefillPopupOthers = "pooltoy-refilled-others";

    [DataField]
    public LocId RefillPopupUser = "pooltoy-refilled-user";

    [DataField]
    public LocId RefillingPopupOthers = "pooltoy-refilling-others";

    [DataField]
    public LocId RefillBreachedPopup = "pooltoy-refill-breached";

    [DataField]
    public LocId RefillEmptyPopup = "pooltoy-refill-empty";

    [DataField]
    public LocId RefillFullPopup = "pooltoy-refill-full";

    /// <summary>
    /// Neither axis of the sprite is ever scaled below this.
    /// </summary>
    [DataField]
    public float MinScale = 0.3f;

    /// <summary>
    /// Humanoid width and height before any deflation, so the shrink can be undone.
    /// </summary>
    [ViewVariables]
    public Vector2? BaseSize;

    [DataField]
    public SoundSpecifier? BreachSound = new SoundPathSpecifier("/Audio/Items/hiss.ogg",
        AudioParams.Default.WithVolume(-4f));

    /// <summary>
    /// Played every <see cref="WarningInterval"/> while air is escaping.
    /// </summary>
    [DataField]
    public SoundSpecifier? DeflatingSound = new SoundPathSpecifier("/Audio/Items/smoke_grenade_smoke.ogg",
        AudioParams.Default.WithVolume(-10f));

    [DataField]
    public SoundSpecifier? FlatSound = new SoundPathSpecifier("/Audio/Effects/balloon-pop.ogg");

    [DataField]
    public SoundSpecifier? SealSound = new SoundPathSpecifier("/Audio/Items/Medical/ointment_end.ogg");

    [DataField]
    public SoundSpecifier? RefillSound = new SoundPathSpecifier("/Audio/Effects/spray.ogg",
        AudioParams.Default.WithVolume(-6f));
}
