namespace Content.Shared.FloofStation;

// Hardlight - Start
/// <summary>
/// Tracks digestion progress for prey currently or recently inside this entity's vore container.
/// </summary>
// Hardlight - End
[RegisterComponent]
public sealed partial class DigestComponent : Component
{
    public Dictionary<EntityUid, float> Health = new();
    public Dictionary<EntityUid, float> Timer = new();
    public HashSet<EntityUid> ActiveDigesting = new();
    public Dictionary<EntityUid, DigestStage> DigestPopupStage = new(); // Hardlight

    [DataField]
    public float Max = 100f;
}

// Hardlight - Start
public enum DigestStage : byte
{
    None,
    Softening,
    Fading,
    LosingShape,
    AlmostGone,
}
// Hardlight - End
