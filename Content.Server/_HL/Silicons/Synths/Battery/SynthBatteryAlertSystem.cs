using Content.Server.Power.Components;
using Content.Shared._HL.Silicons.Synths.Battery;
using Content.Shared._HL.UI;
using Content.Shared.Alert;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._HL.Silicons.Synths.Battery;

public sealed partial class SynthBatteryAlertSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SynthBatterySystem _synthBattery = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan AlertUpdateDelay = TimeSpan.FromSeconds(1);
    private TimeSpan _nextAlertUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<SynthBatteryComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextAlertUpdate)
            return;

        _nextAlertUpdate = _timing.CurTime + AlertUpdateDelay;

        var query = EntityQueryEnumerator<SynthBatteryComponent, BatteryAlertComponent>();
        while (query.MoveNext(out var uid, out var synthBattery, out var alert))
        {
            UpdateBatteryAlert((uid, synthBattery), alert);
        }
    }

    private void OnMapInit(Entity<SynthBatteryComponent> ent, ref MapInitEvent args)
    {
        UpdateBatteryAlert(ent);
    }

    private void UpdateBatteryAlert(Entity<SynthBatteryComponent> ent)
    {
        if (!TryComp<BatteryAlertComponent>(ent.Owner, out var alert))
            return;

        UpdateBatteryAlert(ent, alert);
    }

    private void UpdateBatteryAlert(Entity<SynthBatteryComponent> ent, BatteryAlertComponent alert)
    {
        if (_mobState.IsDead(ent.Owner))
        {
            ClearBatteryAlerts(ent, alert);
            return;
        }

        if (!_synthBattery.TryGetBattery(ent.Owner, out var battery, ent.Comp))
        {
            _alerts.ClearAlert(ent.Owner, alert.BatteryAlert);
            _alerts.ShowAlert(ent.Owner, alert.NoBatteryAlert);
            return;
        }

        var chargePercent = GetChargeSeverity(battery.Value);

        _alerts.ClearAlert(ent.Owner, alert.NoBatteryAlert);
        _alerts.ShowAlert(ent.Owner, alert.BatteryAlert, chargePercent);
    }

    private static short GetChargeSeverity(Entity<BatteryComponent> battery)
    {
        if (battery.Comp.MaxCharge <= 0)
            return 0;

        var chargePercent = (short) MathF.Round(battery.Comp.CurrentCharge / battery.Comp.MaxCharge * 10f);

        if (chargePercent == 0 && battery.Comp.CurrentCharge > 0f)
            chargePercent = 1;

        return (short) Math.Clamp((int) chargePercent, 0, 10);
    }

    private void ClearBatteryAlerts(Entity<SynthBatteryComponent> ent)
    {
        if (!TryComp<BatteryAlertComponent>(ent.Owner, out var alert))
            return;

        ClearBatteryAlerts(ent, alert);
    }

    private void ClearBatteryAlerts(Entity<SynthBatteryComponent> ent, BatteryAlertComponent alert)
    {
        _alerts.ClearAlert(ent.Owner, alert.BatteryAlert);
        _alerts.ClearAlert(ent.Owner, alert.NoBatteryAlert);
    }
}
