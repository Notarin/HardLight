using Robust.Shared.GameStates;

namespace Content.Shared.Eye.Blinding.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VisionGogglesComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public VisionGogglesType Vision;

    [AutoNetworkedField]
    public bool GrantedVision;

    [DataField]
    public float PulseDuration;

    [AutoNetworkedField]
    public float PulseRemaining;

    public float PulseAccumulator;

    [AutoNetworkedField]
    public EntityUid? PulseWearer;

    [DataField]
    public Color PulseColor = Color.FromHex("#F84742");

    [DataField]
    public float PulseLightRadius = 5f;
}

public enum VisionGogglesType : byte
{
    Night,
    Thermal,
}
