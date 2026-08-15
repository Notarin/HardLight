using Content.Server.Speech.Components;
using Content.Shared._HL.Speech;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Server._HL.Speech;

/// <summary>
/// Adds a verb that mutes and unmutes a <see cref="RemoteMuteableComponent"/> entity, leaving it
/// only able to mumble while muted.
/// </summary>
public sealed class RemoteMuteSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RemoteMuteableComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<RemoteMuteableComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == ent.Owner)
            return;

        var user = args.User;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(ent.Comp.Muted ? ent.Comp.UnmuteVerb : ent.Comp.MuteVerb),
            Act = () => SetMuted(ent, !ent.Comp.Muted, user),
        });
    }

    public void SetMuted(Entity<RemoteMuteableComponent> ent, bool muted, EntityUid? user = null)
    {
        if (ent.Comp.Muted == muted)
            return;

        ent.Comp.Muted = muted;
        Dirty(ent);

        if (muted)
            EnsureComp<MumbleAccentComponent>(ent);
        else
            RemComp<MumbleAccentComponent>(ent);

        var popup = Loc.GetString(muted ? ent.Comp.MutedPopup : ent.Comp.UnmutedPopup,
            ("target", Identity.Entity(ent, EntityManager)));

        if (user != null)
            _popup.PopupEntity(popup, ent, user.Value);

        _popup.PopupEntity(popup, ent, ent);
    }
}
