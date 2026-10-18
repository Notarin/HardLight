using Robust.Shared.GameStates;

namespace Content.Shared.Containers.ItemSlots;

/// <summary>
/// Updates the relevant ItemSlots locks based on LockComponent
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ItemSlotsLockComponent : Component
{
    [DataField(required: true)]
    public List<string> Slots = new();
}
