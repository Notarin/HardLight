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
    /// Airloss dealt per second while a breach is open.
    /// </summary>
    [DataField]
    public FixedPoint2 AirlossPerSecond = FixedPoint2.New(0.5);

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
    public Vector2 DeflatedScale = new(0.9f, 0.9f);

    /// <summary>
    /// Sprite scale once fully airless, i.e. in crit.
    /// </summary>
    [DataField]
    public Vector2 FlatScale = new(1.1f, 0.5f);

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
    public LocId FlatPopup = "pooltoy-flat";

    [DataField]
    public LocId SealPopup = "pooltoy-sealed";

    [DataField]
    public SoundSpecifier? BreachSound = new SoundPathSpecifier("/Audio/Items/hiss.ogg");

    /// <summary>
    /// Played every <see cref="WarningInterval"/> while air is escaping.
    /// </summary>
    [DataField]
    public SoundSpecifier? DeflatingSound = new SoundPathSpecifier("/Audio/Effects/spray.ogg");

    [DataField]
    public SoundSpecifier? FlatSound = new SoundPathSpecifier("/Audio/Effects/balloon-pop.ogg");

    [DataField]
    public SoundSpecifier? SealSound = new SoundPathSpecifier("/Audio/Items/Medical/ointment_end.ogg");
}
