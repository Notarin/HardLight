using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.DetailExaminable;

public sealed class DetailExaminableSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DetailExaminableComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(Entity<DetailExaminableComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (Identity.Name(args.Target, EntityManager) != MetaData(args.Target).EntityName)
            return;

        var detailsRange = _examine.IsInDetailsRange(args.User, ent);

        var user = args.User;

        var verb = new ExamineVerb
        {
            Act = () =>
            {
                var markup = new FormattedMessage();
                markup.AddMarkupPermissive(ent.Comp.Content);
                _examine.SendExamineTooltip(user, ent, markup, false, false);
            },
            Text = Loc.GetString("detail-examinable-verb-text"),
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("detail-examinable-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/examine.svg.192dpi.png"))
        };

        args.Verbs.Add(verb);


        var verb1 = new ExamineVerb
        {
            Act = () =>
            {
                var imageFetchResult = new ImageFetchResult(ImageFetchStatus.Loading, [], ent.Comp.CharacterPortraitUrl);
                _examine.SendImageTooltip(user, ent, imageFetchResult, false, false);
            },
            Text = "Image",
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("detail-examinable-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/vv.svg.192dpi.png"))
        };

        args.Verbs.Add(verb1);
    }
}
