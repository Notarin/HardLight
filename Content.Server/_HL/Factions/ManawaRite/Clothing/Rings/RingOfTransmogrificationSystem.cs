using Content.Server.Jittering;
using Content.Shared._HL.Factions.ManawaRite.Clothing.Rings;
using Content.Shared.Clothing;
using Content.Shared.Jittering;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._HL.Factions.ManawaRite.Clothing.Rings;

public sealed class RingOfTransmogrificationSystem : EntitySystem
{
    private static readonly VerbCategory AppearanceCategory = new(
        "Appearance",
        "/Textures/Interface/VerbIcons/group.svg.192dpi.png"
    );

    [Dependency]
    private readonly JitteringSystem _jitter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RingOfTransmogrificationComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<RingOfTransmogrificationComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<RingOfTransmogrificationComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnEquipped(EntityUid uid, RingOfTransmogrificationComponent comp, ClothingGotEquippedEvent args)
    {
        comp.Wearer = args.Wearer;
        ApplyEffect(uid, comp, comp.SelectedEffect);
    }

    private void OnUnequipped(EntityUid uid, RingOfTransmogrificationComponent comp, ClothingGotUnequippedEvent args)
    {
        if (comp.Wearer is { } wearer)
            RemoveEffect(wearer, comp);
        comp.Wearer = null;
    }

    private void OnGetVerbs(EntityUid uid, RingOfTransmogrificationComponent comp, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User != comp.Wearer)
            return;

        foreach (var effect in Enum.GetValues<TransmogrificationEffect>())
        {
            var captured = effect;
            var isCurrent = comp.SelectedEffect == effect;
            args.Verbs.Add(
                new Verb
                {
                    Text = EffectLabel(effect),
                    Category = AppearanceCategory,
                    Act = () => SetEffect(uid, comp, captured),
                    Disabled = isCurrent,
                    Priority = isCurrent ? 1 : 0,
                    Icon = new SpriteSpecifier.Texture(
                        new ResPath("/Textures/Interface/VerbIcons/rejuvenate.svg.192dpi.png")
                    ),
                }
            );
        }
    }

    private void SetEffect(EntityUid uid, RingOfTransmogrificationComponent comp, TransmogrificationEffect effect)
    {
        if (comp.Wearer is not { } wearer)
            return;

        RemoveEffect(wearer, comp);
        comp.SelectedEffect = effect;
        ApplyEffect(uid, comp, effect);
    }

    private void ApplyEffect(EntityUid ring, RingOfTransmogrificationComponent comp, TransmogrificationEffect effect)
    {
        if (comp.Wearer is not { } wearer)
            return;

        var glowComp = EnsureComp<RingGlowEffectComponent>(wearer);
        glowComp.Effect = effect;
        Dirty(wearer, glowComp);

        if (effect == TransmogrificationEffect.Jitter)
        {
            _jitter.AddJitter(wearer, amplitude: 10f, frequency: 4f);
            comp.AddedJitter = true;
        }
    }

    private void RemoveEffect(EntityUid wearer, RingOfTransmogrificationComponent comp)
    {
        if (HasComp<RingGlowEffectComponent>(wearer))
            RemCompDeferred<RingGlowEffectComponent>(wearer);

        if (comp.AddedJitter)
        {
            RemComp<JitteringComponent>(wearer);
            comp.AddedJitter = false;
        }
    }

    private static string EffectLabel(TransmogrificationEffect effect) =>
        effect switch
        {
            TransmogrificationEffect.None => "No effect",
            TransmogrificationEffect.AnomalyFire => "Anomaly: fire",
            TransmogrificationEffect.AnomalyShadow => "Anomaly: shadow",
            TransmogrificationEffect.AnomalyFlora => "Anomaly: flora",
            TransmogrificationEffect.AnomalyFrost => "Anomaly: frost",
            TransmogrificationEffect.AnomalyBluespace => "Anomaly: bluespace",
            TransmogrificationEffect.AnomalyElectricity => "Anomaly: electricity",
            TransmogrificationEffect.AnomalyGravity => "Anomaly: gravity",
            TransmogrificationEffect.AnomalyRock => "Anomaly: rock",
            TransmogrificationEffect.AnomalyFlesh => "Anomaly: flesh",
            TransmogrificationEffect.AnomalyTech => "Anomaly: tech",
            TransmogrificationEffect.Jitter => "Jitter",
            TransmogrificationEffect.Halo => "Halo",
            TransmogrificationEffect.RunicBelt => "Runic belt",
            TransmogrificationEffect.DraconicWings => "Draconic wings",
            TransmogrificationEffect.CyanFireflies => "Cyan fireflies",
            TransmogrificationEffect.WatchingEyes => "Watching eyes",
            _ => effect.ToString(),
        };
}
