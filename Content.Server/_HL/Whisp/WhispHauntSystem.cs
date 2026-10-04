using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Shared._HL.Whisp;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._HL.Whisp;

/// <summary>
/// Manages the progressive psychological buildup and countdown sequence before a whisp spawns on a player.
/// Handles timed popup messages, audio cues, and final entity instantiation.
/// </summary>
public sealed class WhispHauntSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedWhispSystem _whisp = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WhispHauntComponent, ComponentShutdown>(OnHauntShutdown);
    }

    /// <summary>
    /// Handles component shutdown to notify listeners if the haunt was cancelled before completing.
    /// </summary>
    private void OnHauntShutdown(EntityUid uid, WhispHauntComponent comp, ComponentShutdown args)
    {
        if (!comp.Finished)
        {
            var ev = new WhispHauntCancelledEvent(uid);
            RaiseLocalEvent(uid, ref ev, true);
            _adminLog.Add(LogType.Action, LogImpact.Low, $"Whisp haunt cancelled on {ToPrettyString(uid)}.");
        }
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WhispHauntComponent>();
        while (query.MoveNext(out var uid, out var haunt))
        {
            if (Deleted(uid) || Terminating(uid))
                continue;

            // If entity is dead, cancel the haunt
            if (TryComp<MobStateComponent>(uid, out var mobState) && _mobState.IsDead(uid, mobState))
            {
                RemCompDeferred<WhispHauntComponent>(uid);
                continue;
            }

            haunt.Elapsed += frameTime;

            if (haunt.Step < haunt.Steps.Count)
            {
                if (haunt.Elapsed >= haunt.Steps[haunt.Step].Delay)
                {
                    haunt.Elapsed = 0f;
                    haunt.Step++;

                    if (haunt.Step < haunt.Steps.Count)
                    {
                        ShowStepPopup(uid, haunt.Steps[haunt.Step]);
                    }
                    else
                    {
                        FinishHaunt(uid, haunt);
                    }
                }
            }
            else
            {
                FinishHaunt(uid, haunt);
            }
        }
    }

    /// <summary>
    /// Starts a haunt countdown sequence on the target entity.
    /// </summary>
    /// <param name="target">The entity to haunt.</param>
    /// <param name="distance">Initial stalking distance when the whisp spawns.</param>
    /// <param name="speed">Closing speed of the spawned whisp.</param>
    /// <param name="customSteps">Optional custom sequence of steps. Uses default if null.</param>
    /// <returns>True if the haunt was successfully started; false if invalid, already haunted, or dead.</returns>
    public bool StartHaunt(
        EntityUid target,
        float distance = 20f,
        float speed = 0.5f,
        List<WhispHauntStep>? customSteps = null)
    {
        if (Deleted(target) || Terminating(target))
            return false;

        if (HasComp<WhispHauntComponent>(target))
            return false;

        if (TryComp<MobStateComponent>(target, out var mobState) && _mobState.IsDead(target, mobState))
            return false;

        var haunt = EnsureComp<WhispHauntComponent>(target);
        haunt.Step = 0;
        haunt.Elapsed = 0f;
        haunt.WhispDistance = distance;
        haunt.WhispSpeed = speed;

        if (customSteps != null)
            haunt.Steps = customSteps;

        if (haunt.Steps.Count > 0)
        {
            ShowStepPopup(target, haunt.Steps[0]);
        }
        else
        {
            FinishHaunt(target, haunt);
        }

        _adminLog.Add(LogType.Action, LogImpact.High, $"Whisp haunt started on {ToPrettyString(target)}.");
        return true;
    }

    /// <summary>
    /// Cancels an active haunt on the target entity.
    /// </summary>
    /// <param name="target">The entity whose haunt to cancel.</param>
    /// <returns>True if a haunt was found and removed; false otherwise.</returns>
    public bool StopHaunt(EntityUid target)
    {
        if (!HasComp<WhispHauntComponent>(target))
            return false;

        RemComp<WhispHauntComponent>(target);
        return true;
    }

    /// <summary>
    /// Returns whether the entity currently has an active whisp haunt component.
    /// </summary>
    /// <param name="target">Entity to check.</param>
    public bool IsHaunted(EntityUid target)
    {
        return HasComp<WhispHauntComponent>(target);
    }

    /// <summary>
    /// Displays the warning popup and optional chat message for a given haunt stage.
    /// Strictly targets only the haunted player so nearby crew never see or hear the buildup.
    /// </summary>
    private void ShowStepPopup(EntityUid uid, WhispHauntStep step)
    {
        if (!TryComp<ActorComponent>(uid, out var actor) || actor.PlayerSession == null)
            return;

        if (step.Message != null)
        {
            var text = Loc.TryGetString(step.Message.Value, out var loc) ? loc : (string) step.Message.Value;

            // Deliver popup strictly to this player's session only - never broadcast to PVS
            _popup.PopupEntity(text, uid, actor.PlayerSession, step.PopupType);

            if (step.SendChat)
            {
                _chatManager.ChatMessageToOne(ChatChannel.Local, text, text, EntityUid.Invalid, false, actor.PlayerSession.Channel);
            }
        }

        if (step.Sound != null)
        {
            _audio.PlayEntity(step.Sound, Filter.SinglePlayer(actor.PlayerSession), uid, false);
        }
    }

    /// <summary>
    /// Completes the haunt sequence: marks finished, spawns the whisp, removes the haunt component,
    /// and raises <see cref="WhispHauntCompletedEvent"/>.
    /// </summary>
    private void FinishHaunt(EntityUid uid, WhispHauntComponent haunt)
    {
        haunt.Finished = true;
        _whisp.SpawnWhisp(uid, haunt.WhispDistance, haunt.WhispSpeed);
        RemCompDeferred<WhispHauntComponent>(uid);

        var ev = new WhispHauntCompletedEvent(uid);
        RaiseLocalEvent(uid, ref ev, true);

        _adminLog.Add(LogType.Action, LogImpact.High, $"Whisp haunt completed on {ToPrettyString(uid)}; whisp spawned.");
    }
}

/// <summary>
/// Raised directed on an entity when a whisp haunt successfully finishes its countdown and spawns a whisp.
/// </summary>
[ByRefEvent]
public readonly record struct WhispHauntCompletedEvent(EntityUid Target);

/// <summary>
/// Raised directed on an entity when a whisp haunt is cancelled prematurely (e.g. death or admin command).
/// </summary>
[ByRefEvent]
public readonly record struct WhispHauntCancelledEvent(EntityUid Target);
