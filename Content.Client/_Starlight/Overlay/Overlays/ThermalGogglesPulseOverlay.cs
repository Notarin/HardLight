using System.Linq;
using System.Numerics;
using Content.Client.Stealth;
using Content.Shared.Body.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Stealth.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Overlay;

public sealed class ThermalGogglesPulseOverlay : Robust.Client.Graphics.Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly TransformSystem _transform;
    private readonly StealthSystem _stealth;
    private readonly ContainerSystem _container;
    private readonly SharedPointLightSystem _light;
    private readonly List<ThermalVisionRenderEntry> _entries = [];
    private EntityUid? _lightEntity;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    public VisionGogglesComponent? Component;

    public ThermalGogglesPulseOverlay()
    {
        IoCManager.InjectDependencies(this);
        _container = _entity.System<ContainerSystem>();
        _transform = _entity.System<TransformSystem>();
        _stealth = _entity.System<StealthSystem>();
        _light = _entity.System<SharedPointLightSystem>();
        ZIndex = -1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (Component == null || args.Viewport.Eye == null || _player.LocalEntity is not { } player ||
            !_entity.TryGetComponent(player, out TransformComponent? playerXform))
            return;

        var accumulator = Math.Clamp(Component.PulseAccumulator, 0f, Component.PulseDuration);
        var alpha = Component.PulseDuration <= 0f
            ? 1f
            : float.Lerp(1f, 0f, accumulator / Component.PulseDuration);

        if (Component.PulseLightRadius > 0f)
        {
            _lightEntity ??= _entity.SpawnAttachedTo(null, playerXform.Coordinates);
            _transform.SetParent(_lightEntity.Value, player);
            var light = _entity.EnsureComponent<PointLightComponent>(_lightEntity.Value);
            _light.SetRadius(_lightEntity.Value, Component.PulseLightRadius, light);
            _light.SetEnergy(_lightEntity.Value, alpha, light);
            _light.SetColor(_lightEntity.Value, Component.PulseColor, light);
        }
        else
            ResetLight();

        var mapId = args.Viewport.Eye.Position.MapId;
        var eyeRotation = args.Viewport.Eye.Rotation;
        _entries.Clear();

        var entities = _entity.EntityQueryEnumerator<BodyComponent, SpriteComponent, TransformComponent>();
        while (entities.MoveNext(out var uid, out var body, out var sprite, out var xform))
        {
            if (!CanSee(uid, sprite) || !body.ThermalVisibility)
                continue;

            var entity = uid;
            if (_container.TryGetOuterContainer(uid, xform, out var container) &&
                _entity.TryGetComponent<SpriteComponent>(container.Owner, out var ownerSprite) &&
                _entity.TryGetComponent<TransformComponent>(container.Owner, out var ownerXform))
            {
                entity = container.Owner;
                sprite = ownerSprite;
                xform = ownerXform;
            }

            if (_entries.Any(entry => entry.Entity.Owner == entity))
                continue;

            _entries.Add(new ThermalVisionRenderEntry((entity, sprite, xform), mapId, eyeRotation));
        }

        foreach (var entry in _entries)
            Render(entry.Entity, entry.Map, args.WorldHandle, entry.EyeRotation, Component.PulseColor, alpha);

        args.WorldHandle.SetTransform(Matrix3x2.Identity);
    }

    private void Render(Entity<SpriteComponent, TransformComponent> ent, MapId? map,
        DrawingHandleWorld handle, Angle eyeRotation, Color color, float alpha)
    {
        var (uid, sprite, xform) = ent;
        if (xform.MapID != map || !CanSee(uid, sprite))
            return;

        var originalColor = sprite.Color;
        sprite.Color = color.WithAlpha(alpha);
        sprite.Render(handle, eyeRotation, _transform.GetWorldRotation(xform),
            position: _transform.GetWorldPosition(xform));
        sprite.Color = originalColor;
    }

    private bool CanSee(EntityUid uid, SpriteComponent sprite)
    {
        return sprite.Visible && (!_entity.TryGetComponent(uid, out StealthComponent? stealth) ||
                                  _stealth.GetVisibility(uid, stealth) > 0.5f);
    }

    public void ResetLight(bool checkFirstTimePredicted = true)
    {
        if (_lightEntity == null || checkFirstTimePredicted && !_timing.IsFirstTimePredicted)
            return;

        _entity.DeleteEntity(_lightEntity);
        _lightEntity = null;
    }

    private record struct ThermalVisionRenderEntry(
        Entity<SpriteComponent, TransformComponent> Entity,
        MapId? Map,
        Angle EyeRotation);
}
