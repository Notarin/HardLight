using System.Linq;
using Content.Shared._HL.RoundPersistence.SaveBans;
using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._HL.RoundPersistence.SaveBans;

/// <summary>
/// This is the system for showing an icon on examine of an item that is restricted from saving in some fashion.
/// </summary>
public sealed class SaveBanExamineSystem: EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SaveBanApi _saveBanApi = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemComponent, GetVerbsEvent<ExamineVerb>>(OnDetailedExamine);
    }

private void OnDetailedExamine(EntityUid uid, ItemComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        var restrictions = _saveBanApi.CheckForRestrictions(uid);
        if (restrictions == null)
            return;

        var texture = restrictions switch
        {
            SaveBanApi.SaveBanResult.ContainsSaveRestricted containsSaveRestricted => SaveBanApi
                .FlattenRestrictions(containsSaveRestricted)
                .Any(r => r.Ban.Strictness is SaveBanStore.SaveRestrictionStrictness.TotalBan)
                ? "/Textures/_HL/Interface/VerbIcons/containsBanned.png"
                : "/Textures/_HL/Interface/VerbIcons/containsRestricted.png",

            SaveBanApi.SaveBanResult.IsSaveRestricted { Ban.Strictness: SaveBanStore.SaveRestrictionStrictness.TotalBan } =>
                "/Textures/_HL/Interface/VerbIcons/banned.png",

            SaveBanApi.SaveBanResult.IsSaveRestricted { Ban.Strictness: SaveBanStore.SaveRestrictionStrictness.StripBan } =>
                "/Textures/_HL/Interface/VerbIcons/restricted.png",

            _ => throw new ArgumentOutOfRangeException(nameof(uid)),
        };

        var bodyText = restrictions switch
        {
            SaveBanApi.SaveBanResult.ContainsSaveRestricted contains =>
                Loc.GetString(
                    "saveban-hover-body-contains-restricted",
                    ("count", SaveBanApi.FlattenRestrictions(contains).Count())) +
                "\n\n" +
                string.Join('\n', SaveBanApi.FlattenRestrictions(contains).Select(RestrictionText)),

            SaveBanApi.SaveBanResult.IsSaveRestricted { Ban.Strictness: SaveBanStore.SaveRestrictionStrictness.TotalBan } restricted =>
                Loc.GetString(
                    "saveban-hover-body-banned",
                    ("reason", restricted.Ban.Reason)),

            SaveBanApi.SaveBanResult.IsSaveRestricted { Ban.Strictness: SaveBanStore.SaveRestrictionStrictness.StripBan } restricted =>
                Loc.GetString(
                    "saveban-hover-body-restricted",
                    ("reason", restricted.Ban.Reason)),

            _ => throw new ArgumentOutOfRangeException(nameof(restrictions)),
        };

        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(bodyText);

        _examine.AddHoverExamineVerb(args,
            component,
            Loc.GetString("saveban-examine-verb-text"),
            msg.ToMarkup(),
            texture
        );
    }
    private string RestrictionText(SaveBanApi.SaveBanResult.IsSaveRestricted restriction)
    {
        var name = Name(restriction.EntityUid);

        return restriction.Ban.Strictness switch
        {
            SaveBanStore.SaveRestrictionStrictness.TotalBan =>
                Loc.GetString(
                    "saveban-hover-item-listing-restricted",
                    ("variant", "banned"),
                    ("name", name),
                    ("reason", restriction.Ban.Reason)),

            SaveBanStore.SaveRestrictionStrictness.StripBan =>
                Loc.GetString(
                    "saveban-hover-item-listing-restricted",
                    ("variant", "restricted"),
                    ("name", name),
                    ("reason", restriction.Ban.Reason)),

            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
