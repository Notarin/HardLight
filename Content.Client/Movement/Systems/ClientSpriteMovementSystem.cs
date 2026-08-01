using Content.Client.Options;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Content.Shared.Sprite;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Reflection;

namespace Content.Client.Movement.Systems;

/// <summary>
/// Controls the switching of motion and standing still animation
/// </summary>
public sealed class ClientSpriteMovementSystem : SharedSpriteMovementSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private EntityQuery<SpriteComponent> _spriteQuery;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IReflectionManager _reflection = default!;

    public override void Initialize()
    {
        base.Initialize();

        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<SpriteMovementComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnAfterAutoHandleState(Entity<SpriteMovementComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!_spriteQuery.TryGetComponent(ent, out var sprite))
            return;

        if (ent.Comp.IsMoving)
        {
            foreach (var (layer, state) in ent.Comp.MovementLayers)
            {
                if (!TryComp(ent.Owner, out OptionsVisualizerComponent? _) || _sprite.LayerExists((ent.Owner, sprite), layer))
                    _sprite.LayerSetData((ent.Owner, sprite), layer, state);
            }
        }
        else
        {
            foreach (var (layer, state) in ent.Comp.NoMovementLayers)
            {
                if (!TryComp(ent.Owner, out OptionsVisualizerComponent? _) || _sprite.LayerExists((ent.Owner, sprite), layer))
                    _sprite.LayerSetData((ent.Owner, sprite), layer, state);
            }
        }

        // If the entity has a SpriteStateToggle, re-apply its desired state to the configured layer so it persists.
        if (!TryComp<SpriteStateToggleComponent>(ent, out var toggle))
            return;
        if (string.IsNullOrEmpty(toggle.SpriteLayer))
            return;

        var layerIndex = ResolveLayerIndex(ent.Owner, sprite, toggle.SpriteLayer!);
        if (layerIndex < 0)
            return;

        // Read toggle from appearance; if not available yet, don't override the layer to avoid brief reversion.
        if (!_appearance.TryGetData<bool>(ent, SpriteStateToggleVisuals.Toggled, out var value))
            return;
        var enabled = value;

        var isNoballsLayer = toggle.SpriteLayer == "enum.ToggleVisuals.Layer" && _cfg.GetCVar(CCVars.AccessibilityNoballs);
        var effectiveEnabled = isNoballsLayer && enabled ? false : enabled;

        var moving = ent.Comp.IsMoving;
        string? desiredState = null;
        if (moving)
            desiredState = effectiveEnabled ? toggle.MovementStateOn ?? toggle.StateOn : toggle.MovementStateOff ?? toggle.StateOff;
        else
            desiredState = effectiveEnabled ? toggle.StateOn : toggle.StateOff;

        if (!string.IsNullOrEmpty(desiredState))
            sprite.LayerSetState(layerIndex, desiredState!);
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
