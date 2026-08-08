// HardLight

using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgerySpeciesConditionComponent : Component
{
    [DataField]
    public HashSet<ProtoId<SpeciesPrototype>> SpeciesBlacklist = new();

    [DataField]
    public HashSet<ProtoId<SpeciesPrototype>> SpeciesWhitelist = new();
}
