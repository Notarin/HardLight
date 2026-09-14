using Content.Shared._DV.Body.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Robust.Client.GameObjects;

namespace Content.Client._DV.Body;

/// <summary>
/// Colors feathers.
/// </summary>
public sealed class FeatherVisualizer : VisualizerSystem<FeatherComponent>
{
    [Dependency] private readonly ClothingSystem _clothing = default!;

    protected override void OnAppearanceChange(EntityUid uid, FeatherComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<Color>(uid, FeatherVisuals.FeatherColor, out var featherColor, args.Component))
            return;

        // HardLight edit
        if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), FeatherVisualLayers.Feather, out var featherLayer, false))
            SpriteSystem.LayerSetColor((uid, args.Sprite), featherLayer, featherColor);

        if (TryComp<ClothingComponent>(uid, out var clothing))
        {
            foreach (var slotPair in clothing.ClothingVisuals)
            {
                // HardLight edit
                _clothing.SetLayerColor(clothing, slotPair.Key, $"feather-{slotPair.Key}", featherColor);
            }
        }

        if (!AppearanceSystem.TryGetData<Color>(uid, FeatherVisuals.BloodColor, out var bloodColor, args.Component))
            return;

        if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), FeatherVisualLayers.Blood, out var bloodLayer, false))
            SpriteSystem.LayerSetColor((uid, args.Sprite), bloodLayer, bloodColor);
    }
}

public enum FeatherVisualLayers : byte
{
    Feather,
    Blood,
}
