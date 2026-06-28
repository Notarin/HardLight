using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Chapel;

[Serializable, NetSerializable]
public sealed partial class SacrificeDoAfterEvent : SimpleDoAfterEvent { }
