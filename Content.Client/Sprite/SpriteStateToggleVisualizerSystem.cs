using System;
using Content.Shared.CCVar;
using Content.Shared.Sprite;
using Content.Shared.Toggleable;
using Content.Shared.Movement.Components;
using Content.Client.Options;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Reflection;

namespace Content.Client.Sprite;

public sealed class SpriteStateToggleVisualizerSystem : VisualizerSystem<SpriteStateToggleComponent>
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IReflectionManager _reflection = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.AccessibilityNoballs, _ => OnNoballsChanged(), true);
    }

    private void OnNoballsChanged()
    {
        UpdateAllToggleVisuals();
    }

    private void UpdateAllToggleVisuals()
    {
        var query = AllEntityQuery<SpriteStateToggleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            Dirty(uid, component);
        }
    }

    protected override void OnAppearanceChange(EntityUid uid, SpriteStateToggleComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || string.IsNullOrEmpty(component.SpriteLayer))
            return;

        var layerIndex = ResolveLayerIndex(uid, args.Sprite, component.SpriteLayer!);
        if (layerIndex < 0)
            return;

        var enabled = _appearance.TryGetData<bool>(uid, SpriteStateToggleVisuals.Toggled, out var value, args.Component) && value;
        var isNoballsLayer = component.SpriteLayer == "enum.ToggleVisuals.Layer" && _cfg.GetCVar(CCVars.AccessibilityNoballs);

        // If Noballs is enabled, force the toggle layer into the off state when the toggle is enabled.
        // The layer visibility still follows the actual toggle state so it can restore correctly.
        var effectiveEnabled = isNoballsLayer && enabled ? false : enabled;

        // If there's a movement component, prefer the moving or idle variant based on IsMoving.
        var moving = TryComp<SpriteMovementComponent>(uid, out var move) && move.IsMoving;

        string? desiredState = null;
        if (moving)
            desiredState = effectiveEnabled ? component.MovementStateOn ?? component.StateOn : component.MovementStateOff ?? component.StateOff;
        else
            desiredState = effectiveEnabled ? component.StateOn : component.StateOff;

        if (component.VisibleWhenEnabled.HasValue)
        {
            var visible = enabled && component.VisibleWhenEnabled.Value;
            _sprite.LayerSetVisible((uid, args.Sprite), layerIndex, visible);
        }

        if (!string.IsNullOrEmpty(desiredState))
            _sprite.LayerSetRsiState((uid, args.Sprite), layerIndex, new RSI.StateId(desiredState!));
    }

    private int ResolveLayerIndex(EntityUid uid, SpriteComponent sprite, string layerKey)
    {
        if (_reflection.TryParseEnumReference(layerKey, out var @enum))
        {
            if (layerKey == "base" || _sprite.LayerExists((uid, sprite), @enum))
                return _sprite.LayerMapReserve((uid, sprite), @enum);
        }
        else
        {
            if (layerKey == "base" || _sprite.LayerExists((uid, sprite), layerKey))
                return _sprite.LayerMapReserve((uid, sprite), layerKey);
        }

        return -1;
    }
}
