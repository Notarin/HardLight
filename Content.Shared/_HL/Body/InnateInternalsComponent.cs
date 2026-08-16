using Content.Shared.Alert;
using Content.Shared.Atmos;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._HL.Body;

/// <summary>
/// Gives the entity a self contained breathing apparatus. A breath tool and a gas tank are spawned
/// inside the entity and wired into its internals, so it can run internals without a mask or a
/// carried tank.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class InnateInternalsComponent : Component
{
    /// <summary>
    /// Breath tool that gets spawned and connected to the entity's internals.
    /// </summary>
    [DataField]
    public EntProtoId BreathTool = "HLInnateRebreather";

    /// <summary>
    /// Gas tank that gets spawned and made available to the entity's internals.
    /// </summary>
    [DataField]
    public EntProtoId GasTank = "HLInnateAirReservoir";

    /// <summary>
    /// Container the spawned apparatus is kept in.
    /// </summary>
    [DataField]
    public string ContainerId = "innate_internals";

    /// <summary>
    /// How much gas the reservoir holds when full.
    /// </summary>
    [DataField]
    public float TankMoles = 1.230827f;

    /// <summary>
    /// Gas the reservoir is filled with, for entities that breathe oxygen.
    /// </summary>
    [DataField]
    public Gas Gas = Gas.Oxygen;

    /// <summary>
    /// Gas the reservoir is filled with instead, for entities that breathe nitrogen.
    /// </summary>
    [DataField]
    public Gas NitrogenBreatherGas = Gas.Nitrogen;

    /// <summary>
    /// Breathing alert that marks a nitrogen breather, such as a vox or a slime.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> NitrogenBreatherAlert = "LowNitrogen";

    /// <summary>
    /// Species treated as nitrogen breathers when their lungs can't be inspected yet.
    /// </summary>
    [DataField]
    public HashSet<string> NitrogenBreatherSpecies = new() { "Vox", "SlimePerson" };

    /// <summary>
    /// How much gas the frame produces per second while the reservoir isn't being breathed from.
    /// Refills an empty reservoir in a little over five minutes.
    /// </summary>
    [DataField]
    public float RegenMolesPerSecond = 0.004f;

    /// <summary>
    /// How often the reservoir refills.
    /// </summary>
    [DataField]
    public TimeSpan RegenInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextRegen;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? BreathToolEntity;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? GasTankEntity;
}
