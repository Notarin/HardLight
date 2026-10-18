// SPDX-FileCopyrightText: 2025 deltanedas <@deltanedas:kde.org>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Goobstation.Factory.Slots;

/// <summary>
/// An automated FitsInDispenser slot, whose solution can be used by liquid pumps.
/// </summary>
public sealed partial class AutomatedBeakerSlot : AutomationSlot
{
    [DataField]
    public string SlotName = "beakerSlot";

    public ItemSlotsSystem Slots;
    public SharedSolutionContainerSystem Solution;

    public override void Initialize()
    {
        base.Initialize();

        Slots = EntMan.System<ItemSlotsSystem>();
        Solution = EntMan.System<SharedSolutionContainerSystem>();
    }

    public override Entity<SolutionComponent>? GetSolution()
    {
        if (Slots.GetItemOrNull(Owner, SlotName) is not { } beaker)
            return null;

        Solution.TryGetFitsInDispenser(beaker, out var solution, out _);
        return solution;
    }
}
