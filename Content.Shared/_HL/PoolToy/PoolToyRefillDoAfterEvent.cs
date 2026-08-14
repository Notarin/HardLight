using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._HL.PoolToy;

[Serializable, NetSerializable]
public sealed partial class PoolToyRefillDoAfterEvent : SimpleDoAfterEvent
{
}
