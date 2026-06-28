using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.FloofStation.Traits.Events.Components; // HL: Moved this to Shared so the client can use it for verb drawing.

[RegisterComponent, NetworkedComponent, Access(typeof(SharedLewdTraitSystem))]
public sealed partial class PissProducerComponent : Component
{
    [DataField("solutionname")]
    public string SolutionName = "bladder";

    [DataField]
    public ProtoId<ReagentPrototype> ReagentId = "Piss";

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("maxVol")]
    public FixedPoint2 MaxVolume = FixedPoint2.New(25);

    public Entity<SolutionComponent>? Solution = null;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("reVol")]
    public FixedPoint2 QuantityPerUpdate = 5;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("reHunger")]
    public float HungerUsage = 10f;

    [DataField]
    public TimeSpan GrowthDelay = TimeSpan.FromSeconds(10);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextGrowth = TimeSpan.FromSeconds(0);
}
