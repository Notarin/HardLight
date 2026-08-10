using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._HL.Insurance.Components;

[RegisterComponent]
public sealed partial class ParadoxGeneratorComponent : Component
{
    public const string ItemSlotId = "paradox-generator-item";

    [DataField]
    public ItemSlot ItemSlot = new();

    [DataField]
    public float PremiumItemValueMultiplier = 0.10f;

    [DataField]
    public float ClaimItemValueMultiplier = 0.40f;

    [DataField]
    public float PremiumWealthTaxMultiplier = 0.50f;

    [DataField]
    public float ClaimWealthTaxMultiplier = 2.00f;

    [DataField]
    public int WealthTaxThreshold = 100000000;

    [DataField]
    public float WealthTaxBaseRate = 0.01f;

    [DataField]
    public float WealthTaxExcessRate = 0.10f;

    [DataField]
    public int MinPremium = 500;

    [DataField]
    public int MinClaim = 1000;

    [DataField]
    public int MaxPremium = int.MaxValue;

    [DataField]
    public int MaxClaim = int.MaxValue;
}
