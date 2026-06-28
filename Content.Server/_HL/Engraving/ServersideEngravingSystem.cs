using Content.Server.Administration;
using Content.Shared._HL.Engraving;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._HL.Engraving;

public sealed partial class ServersideEngravingSystem : EntitySystem
{
    [Dependency]
    private readonly TagSystem _tag = default!;

    [Dependency]
    private readonly MetaDataSystem _metadata = default!;

    [Dependency]
    private readonly QuickDialogSystem _dialog = default!;
    private static readonly ProtoId<TagPrototype> PreventTag = "PreventLabel"; // if you can't label it you probably shouldn't be able to engrave it,

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EngravingToolComponent, AfterInteractEvent>(EngraveData);
    }

    private void EngraveData(EntityUid uid, EngravingToolComponent tool, AfterInteractEvent args)
    {
        if (tool.RemainingUses == -1)
            tool.RemainingUses = tool.MaxUses;

        if (
            args.Target == null
            || _tag.HasTag(args.Target.Value, PreventTag)
            || TryComp<MindComponent>(args.Target, out _)
        )
            return;

        if (
            HasComp<EngravedDataComponent>(args.Target.Value) && !tool.CanReengrave
            || HasComp<MindComponent>(args.Target.Value)
            || HasComp<MindContainerComponent>(args.Target.Value)
            || HasComp<SSDIndicatorComponent>(args.Target.Value)
            || // specifically catches NPCs
            HasComp<ActorComponent>(args.Target.Value)
        )
            return;

        if (TryComp(args.Target.Value, out MetaDataComponent? meta)) // I'm gonna be honest. If this fails, you probably have bigger problems?
        {
            if (!TryComp<ActorComponent>(args.User, out var actor))
                return;

            if (tool is { Ephemeral: true, RemainingUses: <= 0 })
                return;

            // not going to do xaml for this lmao
            _dialog.OpenDialog(
                actor.PlayerSession,
                "Engrave",
                "Name",
                "Description",
                (string name, LongString description) =>
                {
                    _metadata.SetEntityName(args.Target.Value, name);
                    _metadata.SetEntityDescription(args.Target.Value, description);
                    EnsureComp<EngravedDataComponent>(args.Target.Value, out var data);
                    data.OriginalName = meta.EntityName;
                    data.OriginalDesc = meta.EntityDescription;

                    tool.RemainingUses--;
                }
            );
        }
    }
}
