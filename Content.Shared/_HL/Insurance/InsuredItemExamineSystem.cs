using Content.Shared._HL.Insurance.Components;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._HL.Insurance;

public sealed class InsuredItemExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InsuredItemComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(Entity<InsuredItemComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract)
            return;

        args.Verbs.Add(new ExamineVerb
        {
            Text = Loc.GetString("insured-item-examine-verb"),
            Message = Loc.GetString("insured-item-examine-text"),
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/information.svg.192dpi.png")),
            HoverVerb = true,
            Priority = -1,
        });
    }
}
