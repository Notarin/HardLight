using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.FloofStation.Traits.Events;

[Serializable, NetSerializable]
public sealed partial class CummingDoAfterEvent : SimpleDoAfterEvent
{
    [DataField] public bool SpillOnGround; // HardLight: Cum/Piss/Milk-on verbs
}

