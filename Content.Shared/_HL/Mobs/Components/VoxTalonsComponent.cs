using Content.Shared._HL.Mobs.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._HL.Mobs.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(VoxTalonsSystem))]
public sealed partial class VoxTalonsComponent : Component { }
