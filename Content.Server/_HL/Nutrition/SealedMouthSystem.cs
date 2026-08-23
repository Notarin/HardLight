using Content.Shared._HL.Nutrition;
using Content.Shared.Nutrition;
using Content.Shared.Popups;

namespace Content.Server._HL.Nutrition;

public sealed class SealedMouthSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SealedMouthComponent, IngestionAttemptEvent>(OnIngestAttempt);
    }

    private void OnIngestAttempt(EntityUid uid, SealedMouthComponent component, IngestionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString(component.Popup), uid, uid);
    }
}
