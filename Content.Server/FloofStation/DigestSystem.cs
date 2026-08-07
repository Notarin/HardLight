using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Bed.Cryostorage;
using Content.Server.Mind;
using Content.Server.Nutrition.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Common.Consent;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FloofStation;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.PowerCell.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;

namespace Content.Server.FloofStation;

// Hardlight - Start
/// <summary>
/// Adds digestion verbs for predators and processes consent-gated digestion or passive recovery for tracked prey.
/// Finished digestion sends prey to cryostorage, falling back to deletion only when no cryostorage can be found.
/// </summary>
// Hardlight - End
public sealed class DigestSystem : EntitySystem
{
    // Hardlight - Start
    private const string VoreContainerId = "vore_container";
    private const string DigestConsentId = "Digestable";
    private const int MaxCryostorageCandidates = 16;
    private const float DigestDamagePerSecond = 0.5f;
    private const float PassiveRecoveryPerSecond = 0.1f;
    private const float MinimumRecoveryHunger = 50f;
    private const float MinimumRecoveryBatteryRatio = 0.5f;
    // Hardlight - End

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedConsentSystem _consent = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly CryostorageSystem _cryo = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DigestComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(EntityUid uid, DigestComponent comp, GetVerbsEvent<Verb> args)
    {
        // Hardlight - Start
        var disabled = !_cfg.GetCVar(VoreCVars.DigestionEnabled);
        var wrongUser = args.User != uid;
        var cannotInteract = !args.CanInteract || !args.CanAccess;
        if (disabled || wrongUser || cannotInteract)
            return;

        if (!_container.TryGetContainer(uid, VoreContainerId, out var container) ||
            container.ContainedEntities.Count == 0)
            return;

        foreach (var prey in container.ContainedEntities)
        {
            var isDigesting = comp.ActiveDigesting.Contains(prey);
            var canDigest = _consent.HasConsent(prey, DigestConsentId);

            if (canDigest && !isDigesting)
                AddDigestVerb(args, prey);
            else if (isDigesting)
                AddStopDigestVerb(args, uid, prey);
        }
    }

    private void AddDigestVerb(GetVerbsEvent<Verb> args, EntityUid prey)
    {
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("vore-digest", ("entity", prey)),
            Category = VoreVerbCategory.VoreDigest,
            Act = () => TryDigest(prey)
        });
    }

    private void AddStopDigestVerb(GetVerbsEvent<Verb> args, EntityUid pred, EntityUid prey)
    {
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("vore-stop-digest", ("entity", prey)),
            Category = VoreVerbCategory.VoreDigest,
            Act = () => StopDigest(pred, prey)
        });
    }
    // Hardlight - End

    private void TryDigest(EntityUid prey)
    {
        if (!_container.TryGetContainingContainer(prey, out var container))
            return;

        var pred = container.Owner;
        if (!TryComp<DigestComponent>(pred, out var comp))
            return;

        _popup.PopupEntity(Loc.GetString("vore-digest-start", ("entity", pred)), pred, pred);
        _popup.PopupEntity(Loc.GetString("vore-digest-start", ("entity", pred)), prey, prey, PopupType.LargeCaution);

        comp.Health.TryAdd(prey, comp.Max);
        comp.ActiveDigesting.Add(prey);
        comp.Timer[prey] = 0f;
    }

    private void StopDigest(EntityUid pred, EntityUid prey)
    {
        if (!TryComp<DigestComponent>(pred, out var comp))
            return;

        comp.ActiveDigesting.Remove(prey);
        comp.Timer[prey] = 0f;

        _popup.PopupEntity(Loc.GetString("vore-digest-stop", ("entity", pred)), pred, pred);
        _popup.PopupEntity(Loc.GetString("vore-digest-stop", ("entity", pred)), prey, prey);
    }

    private void FinishDigest(EntityUid prey)
    {
        if (_container.TryGetContainingContainer(prey, out var container))
            _popup.PopupEntity(Loc.GetString("vore-digested-owner-1", ("entity", prey)), container.Owner, container.Owner);

        SendToCryo(prey);
    }

    private void SendToCryo(EntityUid prey)
    {
        // Hardlight - Start
        var query = EntityQueryEnumerator<CryostorageComponent>();
        var checkedCandidates = 0;

        while (query.MoveNext(out var cryoUid, out _) && checkedCandidates++ < MaxCryostorageCandidates)
        {
            var contained = EnsureComp<CryostorageContainedComponent>(prey);
            contained.Cryostorage = cryoUid;
            _mind.TryGetMind(prey, out _, out var mindComp);
            _cryo.HandleEnterCryostorage((prey, contained), mindComp?.UserId);
            return;
        }

        QueueDel(prey);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (pred, comp) in GetDigestPredators())
            ProcessPredator(frameTime, pred, comp);
    }

    private List<(EntityUid Pred, DigestComponent Comp)> GetDigestPredators()
    {
        var preds = new List<(EntityUid Pred, DigestComponent Comp)>();
        var query = EntityQueryEnumerator<DigestComponent>();

        while (query.MoveNext(out var pred, out var comp))
            preds.Add((pred, comp));

        return preds;
    }

    private void ProcessPredator(float frameTime, EntityUid pred, DigestComponent comp)
    {
        var finished = new List<EntityUid>();

        foreach (var prey in comp.Health.Keys.ToList())
        {
            if (!AdvanceTimer(frameTime, prey, comp))
                continue;

            if (!Exists(prey))
            {
                finished.Add(prey);
                continue;
            }

            if (comp.ActiveDigesting.Contains(prey))
            {
                if (ProcessActiveDigestion(prey, comp))
                    finished.Add(prey);
            }
            else
            {
                ProcessPassiveRecovery(prey, comp);
            }
        }

        foreach (var prey in finished)
            CleanupFinishedPrey(prey, comp);
    }

    private bool AdvanceTimer(float frameTime, EntityUid prey, DigestComponent comp)
    {
        comp.Timer[prey] += frameTime;
        if (comp.Timer[prey] < 1f)
            return false;

        comp.Timer[prey] -= 1f;
        return true;
    }

    private bool ProcessActiveDigestion(EntityUid prey, DigestComponent comp)
    {
        if (!TryGetDigestContainer(prey, out var container))
        {
            comp.ActiveDigesting.Remove(prey);
            comp.Timer[prey] = 0f;
            return false;
        }

        comp.Health[prey] -= DigestDamagePerSecond;
        ShowDigestPopup(prey, comp);
        FeedPredator(container.Owner);

        return comp.Health[prey] <= 0f;
    }

    private bool TryGetDigestContainer(EntityUid prey, [NotNullWhen(true)] out BaseContainer? container)
    {
        return _container.TryGetContainingContainer(prey, out container) &&
               container.ID == VoreContainerId &&
               _consent.HasConsent(prey, DigestConsentId);
    }

    private void FeedPredator(EntityUid pred)
    {
        if (TryComp<HungerComponent>(pred, out var hunger))
        {
            _hunger.ModifyHunger(pred, 1f, hunger);
            return;
        }

        if (TryComp<BatteryComponent>(pred, out var battery))
        {
            _battery.SetCharge(pred, battery.CurrentCharge + 2f, battery);
            return;
        }

        if (TryGetPowerCellBattery(pred, out var cellUid, out var batteryComp))
            _battery.SetCharge(cellUid, batteryComp.CurrentCharge + 2f, batteryComp);
    }

    private void ProcessPassiveRecovery(EntityUid prey, DigestComponent comp)
    {
        if (comp.Health[prey] >= comp.Max)
            return;

        if (!TrySpendRecoveryResource(prey))
            return;

        comp.Health[prey] += PassiveRecoveryPerSecond;
    }

    private bool TrySpendRecoveryResource(EntityUid prey)
    {
        if (TryComp<HungerComponent>(prey, out var hunger))
        {
            if (_hunger.GetHunger(hunger) <= MinimumRecoveryHunger)
                return false;

            _hunger.ModifyHunger(prey, -1f, hunger);
            return true;
        }

        if (TryComp<BatteryComponent>(prey, out var battery))
            return TrySpendBattery(prey, battery, 1f);

        return TryGetPowerCellBattery(prey, out var cellUid, out var batteryComp) &&
               TrySpendBattery(cellUid, batteryComp, 2f);
    }

    private bool TrySpendBattery(EntityUid uid, BatteryComponent battery, float amount)
    {
        if (battery.CurrentCharge <= battery.MaxCharge * MinimumRecoveryBatteryRatio)
            return false;

        _battery.SetCharge(uid, battery.CurrentCharge - amount, battery);
        return true;
    }

    private bool TryGetPowerCellBattery(EntityUid uid, out EntityUid cellUid, out BatteryComponent battery)
    {
        cellUid = default;
        battery = default!;

        if (!TryComp<PowerCellSlotComponent>(uid, out var batterySlot) ||
            !_itemSlots.TryGetSlot(uid, batterySlot.CellSlotId, out var itemSlot) ||
            itemSlot.Item is not { } cell ||
            !TryComp<BatteryComponent>(cell, out var batteryComp))
        {
            return false;
        }

        cellUid = cell;
        battery = batteryComp;
        return true;
    }

    private void CleanupFinishedPrey(EntityUid prey, DigestComponent comp)
    {
        comp.Health.Remove(prey);
        comp.Timer.Remove(prey);
        comp.ActiveDigesting.Remove(prey);
        comp.DigestPopupStage.Remove(prey);
        FinishDigest(prey);
    }

    private void ShowDigestPopup(EntityUid prey, DigestComponent comp)
    {
        var stage = GetDigestStage(comp.Health[prey] / comp.Max);
        if (stage == DigestStage.None)
            return;
        if (comp.DigestPopupStage.TryGetValue(prey, out var lastStage) && lastStage >= stage)
            return;

        comp.DigestPopupStage[prey] = stage;

        var message = stage switch
        {
            DigestStage.Softening => "vore-digest-stage-1",
            DigestStage.Fading => "vore-digest-stage-2",
            DigestStage.LosingShape => "vore-digest-stage-3",
            DigestStage.AlmostGone => "vore-digest-stage-4",
            _ => null
        };

        if (message != null)
            _popup.PopupEntity(Loc.GetString(message), prey, prey);
    }

    private static DigestStage GetDigestStage(float healthPercent)
    {
        return healthPercent switch
        {
            <= 0.10f => DigestStage.AlmostGone,
            <= 0.25f => DigestStage.LosingShape,
            <= 0.50f => DigestStage.Fading,
            <= 0.75f => DigestStage.Softening,
            _ => DigestStage.None
        };
    }
    // Hardlight - End
}
