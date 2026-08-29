using Robust.Shared.Prototypes;

namespace Content.Shared._HL.RoundPersistence.SaveBans;

/// <summary>
/// A prototype for a filter to match to ban items.
/// </summary>
[Prototype]
public sealed partial class SaveBanPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = null!;

    /// <summary>
    /// A documented reason for an items ban. A documented Chesterton's fence. Ideally as specific as possible.
    /// </summary>
    [DataField]
    public required string Reason;

    /// <summary>
    /// A currently unused context for a ban. Will be a bit composable filter for which contexts the ban applies.
    /// </summary>
    public enum SaveBanContext: byte
    {
        ShipSave = 1 << 0,
        ApartmentSave = 1 << 1,
        InsuranceSave = 1 << 2,
    }
    /// <summary>
    /// <see cref="SaveBanContext"/>
    /// </summary>
    [DataField]
    public SaveBanContext Context = (SaveBanContext)byte.MaxValue;

    /// <summary>
    /// The prototypes to ban. It is preferable to use components instead of prototypes. A ban should be as specific as possible.
    /// </summary>
    [DataField]
    public IReadOnlyList<string> Prototypes = [];

    /// <summary>
    /// The components to ban.
    /// </summary>
    [DataField]
    public IReadOnlyList<string> Components = [];
}
