using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Goobstation.Factory.Slots;

/// <summary>
/// Abstraction over an <see cref="ItemSlot"/> on the machine.
/// </summary>
public sealed partial class AutomatedItemSlot : AutomationSlot
{
    /// <summary>
    /// The name of the slot to automate.
    /// </summary>
    [DataField(required: true)]
    public string SlotId = string.Empty;

    public ItemSlotsSystem Slots;

    private ItemSlot? _slot;

    [ViewVariables]
    public ItemSlot Slot
    {
        get
        {
            if (_slot is { } slot)
                return slot;

            if (Slots.TryGetSlot(Owner, SlotId, out _slot))
                return _slot;

            throw new InvalidOperationException($"Entity {EntMan.ToPrettyString(Owner)} had no item slot {SlotId}");
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        Slots = EntMan.System<ItemSlotsSystem>();
    }

    public override bool Insert(EntityUid item)
    {
        return base.Insert(item) &&
            Slots.TryInsert(Owner, Slot, item, user: null);
    }

    public override bool CanInsert(EntityUid item)
    {
        return base.CanInsert(item) &&
            Slots.CanInsert(Owner, usedUid: item, user: null, Slot);
    }

    public override EntityUid? GetItem(EntityUid? filter)
    {
        if (Slot.Item is not { } item || Filter.IsBlocked(filter, item))
            return null;

        return item;
    }
}
