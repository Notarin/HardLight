using Content.Server._NF.Projectile.Components;
using Content.Server.Chat.Systems;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._NF.Projectile.EntitySystems;

public sealed partial class BlindOnProjectileHitSystem : EntitySystem
{
    [Dependency]
    private readonly StatusEffectsSystem _statusEffectsSystem = default!;

    [Dependency]
    private readonly BlindableSystem _blindingSystem = default!;

    [Dependency]
    private readonly IRobustRandom _random = default!;

    [Dependency]
    private readonly ChatSystem _chat = default!;

    private readonly ProtoId<EmotePrototype> _screamEmoteId = "Scream";

    public override void Initialize()
    {
        SubscribeLocalEvent<BlindOnProjectileHitComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnProjectileHit(Entity<BlindOnProjectileHitComponent> ent, ref ProjectileHitEvent args)
    {
        if (!TryComp<BlindableComponent>(args.Target, out var blindable) || blindable.IsBlind)
            return;

        if (!_random.Prob(ent.Comp.Prob))
            return;

        var eyeProtectionEv = new GetEyeProtectionEvent();
        RaiseLocalEvent(args.Target, eyeProtectionEv);

        var time = (float)(ent.Comp.BlindTime - eyeProtectionEv.Protection).TotalSeconds;
        if (time <= 0)
            return;

        _chat.TryEmoteWithoutChat(args.Target, _screamEmoteId);

        // Add permanent eye damage if they had zero protection, also somewhat scale their temporary blindness by
        // how much damage they already accumulated.
        _blindingSystem.AdjustEyeDamage((args.Target, blindable), 1);
        var statusTimeSpan = TimeSpan.FromSeconds(time * MathF.Sqrt(blindable.EyeDamage));
        _statusEffectsSystem.TryAddStatusEffect(
            args.Target,
            TemporaryBlindnessSystem.BlindingStatusEffect,
            statusTimeSpan,
            false,
            TemporaryBlindnessSystem.BlindingStatusEffect
        );
    }
}
