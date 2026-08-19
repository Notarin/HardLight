using System.Linq;
using Content.Server.Popups;
using Content.Shared._HL.RoundPersistence.SaveBans;
using Content.Shared._HL.Shipyard;
using Robust.Shared.Containers;

namespace Content.Server._HL.RoundPersistence.SaveBans;

/// <summary>
/// Prevents players from putting things that are stash banned into bluespace stashes.
/// </summary>
public sealed class StashBanSystem : EntitySystem
{
    [Dependency] private readonly SaveBanApi _saveBanApi = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HLPersistOnShipSaveComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
    }

    private void OnInsertAttempt(EntityUid uid, HLPersistOnShipSaveComponent component, ContainerIsInsertingAttemptEvent args)
    {
        var banned = _saveBanApi.CheckForRestrictions(args.EntityUid);
        switch (banned)
        {
            case SaveBanApi.SaveBanResult.IsSaveRestricted isSaveRestricted:
                if (isSaveRestricted.Ban.Strictness is SaveBanStore.SaveRestrictionStrictness.TotalBan)
                    Reject(args);
                break;
            case SaveBanApi.SaveBanResult.ContainsSaveRestricted containsSaveRestricted:
                if (SaveBanApi.FlattenRestrictions(containsSaveRestricted)
                    .Any(r => r.Ban.Strictness is SaveBanStore.SaveRestrictionStrictness.TotalBan))
                    Reject(args);
                break;
        }

        return;

        void Reject(ContainerIsInsertingAttemptEvent containerIsInsertingAttemptEvent)
        {
            _popup.PopupEntity(Loc.GetString("saveban-stash-rejected"), containerIsInsertingAttemptEvent.EntityUid);
            containerIsInsertingAttemptEvent.Cancel();
        }
    }

}
