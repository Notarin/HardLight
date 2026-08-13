using Robust.Shared.Serialization;

namespace Content.Shared._HL.Insurance;

[Serializable, NetSerializable]
public enum ParadoxGeneratorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ParadoxGeneratorBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly int Balance;
    public readonly bool HasInsertedItem;
    public readonly string? InsertedItemName;
    public readonly int InsertedItemValue;
    public readonly int InsertedItemPremium;
    public readonly string? InsertedItemCannotInsureReason;
    public readonly List<InsuranceListingState> Listings;

    public ParadoxGeneratorBoundUserInterfaceState(
        int balance,
        bool hasInsertedItem,
        string? insertedItemName,
        int insertedItemValue,
        int insertedItemPremium,
        string? insertedItemCannotInsureReason,
        List<InsuranceListingState> listings)
    {
        Balance = balance;
        HasInsertedItem = hasInsertedItem;
        InsertedItemName = insertedItemName;
        InsertedItemValue = insertedItemValue;
        InsertedItemPremium = insertedItemPremium;
        InsertedItemCannotInsureReason = insertedItemCannotInsureReason;
        Listings = listings;
    }
}

[Serializable, NetSerializable]
public sealed class ParadoxGeneratorUiStateMessage : EntityEventArgs
{
    public readonly NetEntity Generator;
    public readonly ParadoxGeneratorBoundUserInterfaceState State;

    public ParadoxGeneratorUiStateMessage(NetEntity generator, ParadoxGeneratorBoundUserInterfaceState state)
    {
        Generator = generator;
        State = state;
    }
}

[Serializable, NetSerializable]
public sealed class InsuranceListingState
{
    public readonly string PolicyId;
    public readonly string Name;
    public readonly int InsuredValue;
    public readonly int ClaimCost;
    public readonly long Generation;
    public readonly bool LiveCurrentCopyExists;
    public readonly string? CannotClaimReason;

    public InsuranceListingState(
        string policyId,
        string name,
        int insuredValue,
        int claimCost,
        long generation,
        bool liveCurrentCopyExists,
        string? cannotClaimReason)
    {
        PolicyId = policyId;
        Name = name;
        InsuredValue = insuredValue;
        ClaimCost = claimCost;
        Generation = generation;
        LiveCurrentCopyExists = liveCurrentCopyExists;
        CannotClaimReason = cannotClaimReason;
    }
}

[Serializable, NetSerializable]
public sealed class ParadoxGeneratorRefreshMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class ParadoxGeneratorInsureMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class ParadoxGeneratorClaimMessage : BoundUserInterfaceMessage
{
    public readonly string PolicyId;

    public ParadoxGeneratorClaimMessage(string policyId)
    {
        PolicyId = policyId;
    }
}

[Serializable, NetSerializable]
public sealed class ParadoxGeneratorUninsureMessage : BoundUserInterfaceMessage
{
    public readonly string PolicyId;

    public ParadoxGeneratorUninsureMessage(string policyId)
    {
        PolicyId = policyId;
    }
}
