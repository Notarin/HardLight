using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.Shared._Goobstation.Factory.Slots;

/// <summary>
/// Abstraction over a specific hand of the machine.
/// </summary>
public sealed partial class AutomatedHand : AutomationSlot
{
    /// <summary>
    /// The name of the hand to use
    /// </summary>
    [DataField(required: true)]
    public string HandName = string.Empty;

    public SharedHandsSystem Hands;

    private Hand? _hand;

    [ViewVariables]
    public Hand? Hand
    {
        get
        {
            if (_hand != null)
                return _hand;

            Hands.TryGetHand(Owner, HandName, out _hand);
            return _hand;
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        Hands = EntMan.System<SharedHandsSystem>();
    }

    public override bool Insert(EntityUid item)
    {
        return Hand is { } hand
            && base.Insert(item)
            && Hands.TryPickup(Owner, item, hand);
    }

    public override bool CanInsert(EntityUid item)
    {
        return Hand is { } hand
            && base.CanInsert(item)
            && Hands.CanPickupToHand(Owner, item, hand);
    }

    public override EntityUid? GetItem(EntityUid? filter)
    {
        if (Hand?.HeldEntity is not { } item
            || Filter.IsBlocked(filter, item))
            return null;

        return item;
    }
}
