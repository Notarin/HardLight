using Content.Shared.CM14.Xenos;
using Content.Shared.CM14.Xenos.Rest;
using Content.Shared.Mobs;
using Content.Client.DamageState;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.CM14.Xenos;

public sealed class XenoVisualizerSystem : VisualizerSystem<XenoComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, XenoComponent component, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;

        if (sprite is not { BaseRSI: { } rsi })
            return;

        // Prefer the XenoVisualLayers.Base mapping, but fall back to DamageStateVisualLayers.Base
        if (!SpriteSystem.LayerMapTryGet((uid, sprite), XenoVisualLayers.Base, out var layer, false) &&
            !SpriteSystem.LayerMapTryGet((uid, sprite), DamageStateVisualLayers.Base, out layer, false))
            return;

        var state = CompOrNull<MobStateComponent>(uid)?.CurrentState;

        switch (state)
        {
            case MobState.Critical:
                ClearDrawDepth((uid, component, sprite));

                if (rsi.TryGetState("crit", out _))
                    SpriteSystem.LayerSetRsiState((uid, sprite), layer, "crit");
                break;
            case MobState.Dead:
                SetDrawDepth((uid, component, sprite));

                if (rsi.TryGetState("dead", out _))
                    SpriteSystem.LayerSetRsiState((uid, sprite), layer, "dead");
                break;
            default:
                ClearDrawDepth((uid, component, sprite));

                if (args.AppearanceData.TryGetValue(XenoVisualLayers.Base, out var resting) &&
                    resting is XenoRestState.Resting)
                {
                    if (rsi.TryGetState("sleeping", out _))
                        SpriteSystem.LayerSetRsiState((uid, sprite), layer, "sleeping");
                    break;
                }

                if (rsi.TryGetState("alive", out _))
                    SpriteSystem.LayerSetRsiState((uid, sprite), layer, "alive");
                break;
        }
    }

    private void SetDrawDepth(Entity<XenoComponent, SpriteComponent> ent)
    {
        var (uid, xeno, sprite) = ent;
        if (sprite.DrawDepth > (int)DrawDepth.DeadMobs)
        {
            xeno.OriginalDrawDepth = sprite.DrawDepth;
            SpriteSystem.SetDrawDepth((uid, sprite), (int)DrawDepth.DeadMobs);
        }
    }

    private void ClearDrawDepth(Entity<XenoComponent, SpriteComponent> ent)
    {
        var (uid, xeno, sprite) = ent;
        if (xeno.OriginalDrawDepth != null)
        {
            SpriteSystem.SetDrawDepth((uid, sprite), xeno.OriginalDrawDepth.Value);
            xeno.OriginalDrawDepth = null;
        }
    }
}
