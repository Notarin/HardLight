using Content.Shared._HL.Factions.ManawaRite.Clothing.Rings;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._HL.Factions.ManawaRite.Clothing.Rings;

public sealed class RingGlowEffectSystem : EntitySystem
{
    [Dependency]
    private readonly SpriteSystem _sprite = default!;

    private const string LayerKey = "ring_transmogrification_layer";
    private const string LayerKey2 = "ring_transmogrification_layer_2";

    private static readonly ResPath AnomalyRsi = new("Structures/Specific/Anomalies/inner_anom_layer.rsi");
    private static readonly ResPath HaloRsi = new("Clothing/Head/Hats/holyhatmelon.rsi");
    private static readonly ResPath RunicBeltRsi = new("_NF/Clothing/Belt/cult_force_field.rsi");
    private static readonly ResPath WingsRsi = new("_RMC14/Mobs/Customization/reptilian.rsi");
    private static readonly ResPath FireflyRsi = new("_Impstation/Mobs/Customization/animatedmarkings.rsi");
    private static readonly ResPath WatchingEyesRsi = new(
        "_HL/Factions/ManawaRite/Mobs/Customization/watching_eyes.rsi"
    );

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RingGlowEffectComponent, AfterAutoHandleStateEvent>(OnStateHandled);
        SubscribeLocalEvent<RingGlowEffectComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStateHandled(Entity<RingGlowEffectComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyOverlay(ent);
    }

    private void OnShutdown(Entity<RingGlowEffectComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        HideLayer(ent.Owner, sprite, LayerKey);
        HideLayer(ent.Owner, sprite, LayerKey2);
    }

    private void ApplyOverlay(Entity<RingGlowEffectComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var effect = ent.Comp.Effect;

        // Always hide second layer first; re-show only for multi-layer effects.
        HideLayer(ent.Owner, sprite, LayerKey2);

        var spec = GetSpriteOverlay(effect);
        if (spec == null)
        {
            HideLayer(ent.Owner, sprite, LayerKey);
            return;
        }

        SetLayer(
            ent.Owner,
            sprite,
            LayerKey,
            spec,
            tint: GetLayerTint(ent.Owner, effect, layer: 0),
            unshaded: IsUnshaded(effect)
        );

        // Wings need a second layer.
        if (effect == TransmogrificationEffect.DraconicWings)
        {
            SetLayer(
                ent.Owner,
                sprite,
                LayerKey2,
                new SpriteSpecifier.Rsi(WingsRsi, "body_dragonwings_membrane"),
                tint: GetLayerTint(ent.Owner, effect, layer: 1),
                unshaded: false
            );
        }
    }

    private void SetLayer(
        EntityUid uid,
        SpriteComponent sprite,
        string key,
        SpriteSpecifier spec,
        Color? tint,
        bool unshaded
    )
    {
        var idx = _sprite.LayerMapReserve((uid, sprite), key);
        _sprite.LayerSetSprite((uid, sprite), idx, spec);
        _sprite.LayerSetVisible((uid, sprite), idx, true);
        if (tint.HasValue)
            _sprite.LayerSetColor((uid, sprite), idx, tint.Value);
        if (unshaded)
            sprite.LayerSetShader(idx, "unshaded");
    }

    private void HideLayer(EntityUid uid, SpriteComponent sprite, string key)
    {
        if (_sprite.LayerMapTryGet((uid, sprite), key, out var idx, false))
            _sprite.LayerSetVisible((uid, sprite), idx, false);
    }

    private Color? GetLayerTint(EntityUid uid, TransmogrificationEffect effect, int layer)
    {
        switch (effect)
        {
            case TransmogrificationEffect.DraconicWings:
            {
                var skin = TryComp<HumanoidAppearanceComponent>(uid, out var humanoid)
                    ? humanoid.SkinColor
                    : Color.White;
                if (layer == 0)
                    return skin;
                // Secondary layer: skin tone with -10 saturation, +20 value.
                var hsv = Color.ToHsv(skin);
                hsv.Y = Math.Clamp(hsv.Y - 0.10f, 0f, 1f);
                hsv.Z = Math.Clamp(hsv.Z + 0.20f, 0f, 1f);
                return Color.FromHsv(hsv);
            }
            case TransmogrificationEffect.CyanFireflies:
                return Color.FromHex("#00FFFF");
            default:
                return null;
        }
    }

    private static SpriteSpecifier? GetSpriteOverlay(TransmogrificationEffect effect) =>
        effect switch
        {
            TransmogrificationEffect.AnomalyFire => new SpriteSpecifier.Rsi(AnomalyRsi, "fire"),
            TransmogrificationEffect.AnomalyShadow => new SpriteSpecifier.Rsi(AnomalyRsi, "shadow"),
            TransmogrificationEffect.AnomalyFlora => new SpriteSpecifier.Rsi(AnomalyRsi, "flora"),
            TransmogrificationEffect.AnomalyFrost => new SpriteSpecifier.Rsi(AnomalyRsi, "frost"),
            TransmogrificationEffect.AnomalyBluespace => new SpriteSpecifier.Rsi(AnomalyRsi, "bluespace"),
            TransmogrificationEffect.AnomalyElectricity => new SpriteSpecifier.Rsi(AnomalyRsi, "shock"),
            TransmogrificationEffect.AnomalyGravity => new SpriteSpecifier.Rsi(AnomalyRsi, "grav"),
            TransmogrificationEffect.AnomalyRock => new SpriteSpecifier.Rsi(AnomalyRsi, "rock"),
            TransmogrificationEffect.AnomalyFlesh => new SpriteSpecifier.Rsi(AnomalyRsi, "flesh"),
            TransmogrificationEffect.AnomalyTech => new SpriteSpecifier.Rsi(AnomalyRsi, "tech"),
            TransmogrificationEffect.Halo => new SpriteSpecifier.Rsi(HaloRsi, "equipped-HELMET"),
            TransmogrificationEffect.RunicBelt => new SpriteSpecifier.Rsi(RunicBeltRsi, "equipped-BELT"),
            TransmogrificationEffect.DraconicWings => new SpriteSpecifier.Rsi(WingsRsi, "body_dragonwings"),
            TransmogrificationEffect.CyanFireflies => new SpriteSpecifier.Rsi(FireflyRsi, "dionafirefly"),
            TransmogrificationEffect.WatchingEyes => new SpriteSpecifier.Rsi(WatchingEyesRsi, "watching-eyes"),
            _ => null,
        };

    private static bool IsUnshaded(TransmogrificationEffect effect) =>
        effect switch
        {
            TransmogrificationEffect.AnomalyFire => true,
            TransmogrificationEffect.AnomalyShadow => true,
            TransmogrificationEffect.AnomalyFlora => true,
            TransmogrificationEffect.AnomalyFrost => true,
            TransmogrificationEffect.AnomalyBluespace => true,
            TransmogrificationEffect.AnomalyElectricity => true,
            TransmogrificationEffect.AnomalyGravity => true,
            TransmogrificationEffect.AnomalyRock => true,
            TransmogrificationEffect.AnomalyFlesh => true,
            TransmogrificationEffect.AnomalyTech => true,
            TransmogrificationEffect.Halo => true,
            TransmogrificationEffect.RunicBelt => true,
            TransmogrificationEffect.CyanFireflies => true,
            TransmogrificationEffect.WatchingEyes => true,
            _ => false,
        };
}
