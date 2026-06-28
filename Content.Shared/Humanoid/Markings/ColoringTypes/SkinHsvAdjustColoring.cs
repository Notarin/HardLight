using System.Numerics;

namespace Content.Shared.Humanoid.Markings;

/// <summary>
/// Colors a marking layer using the character's skin color, shifted by the given HSV offsets.
/// Saturation and value are on a 0–100 scale; hue is in degrees.
/// </summary>
public sealed partial class SkinHsvAdjustColoring : LayerColoringType
{
    [DataField]
    public float HueAdjust { get; private set; } = 0f;

    [DataField]
    public float SaturationAdjust { get; private set; } = 0f;

    [DataField]
    public float ValueAdjust { get; private set; } = 0f;

    public override Color? GetCleanColor(Color? skin, Color? eyes, MarkingSet markingSet)
    {
        if (skin is not { } s)
            return null;

        var hsv = Color.ToHsv(s);
        hsv.X = (hsv.X + HueAdjust / 360f) % 1f;
        if (hsv.X < 0f)
            hsv.X += 1f;
        hsv.Y = Math.Clamp(hsv.Y + SaturationAdjust / 100f, 0f, 1f);
        hsv.Z = Math.Clamp(hsv.Z + ValueAdjust / 100f, 0f, 1f);
        return Color.FromHsv(hsv);
    }
}
