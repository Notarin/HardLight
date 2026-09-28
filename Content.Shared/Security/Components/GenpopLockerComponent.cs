using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Security.Components;

/// <summary>
/// This is used for a locker that automatically sets up and handles a <see cref="GenpopIdCardComponent"/>
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GenpopLockerComponent : Component
{
    public const int MaxCrimeLength = 48;

    /// <summary>
    /// The <see cref="GenpopIdCardComponent"/> that this locker is currently associated with.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedId;

    /// <summary>
    /// The Prototype spawned.
    /// </summary>
    [DataField("IdCardProto", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string IdCardProto = "PrisonerIDCard";
}

[Serializable, NetSerializable]
public sealed class GenpopLockerIdConfiguredMessage : BoundUserInterfaceMessage
{
    public string Name;
    public float Sentence;
    public string Crime;

    public GenpopLockerIdConfiguredMessage(string name, float sentence, string crime)
    {
        Name = name;
        Sentence = sentence;
        Crime = crime;
    }
}

[Serializable, NetSerializable]
public enum GenpopLockerUiKey : byte
{
    Key
}
