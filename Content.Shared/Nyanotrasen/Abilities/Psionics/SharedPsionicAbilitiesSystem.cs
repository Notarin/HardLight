using System;
using Content.Shared.Abilities.Psionics;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Psionics.Glimmer;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared.Nyanotrasen.Abilities.Psionics
{
    public sealed class SharedPsionicAbilitiesSystem : EntitySystem
    {
        [Dependency]
        private readonly SharedActionsSystem _actions = default!;

        [Dependency]
        private readonly EntityLookupSystem _lookup = default!;

        [Dependency]
        private readonly SharedPopupSystem _popups = default!;

        [Dependency]
        private readonly ISharedAdminLogManager _adminLogger = default!;

        [Dependency]
        private readonly GlimmerSystem _glimmerSystem = default!;

        [Dependency]
        private readonly IRobustRandom _robustRandom = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PsionicsDisabledComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<PsionicsDisabledComponent, ComponentShutdown>(OnShutdown);
            SubscribeLocalEvent<PsionicComponent, PsionicPowerUsedEvent>(OnPowerUsed);

            SubscribeLocalEvent<PsionicComponent, MobStateChangedEvent>(OnMobStateChanged);
        }

        private void OnPowerUsed(EntityUid uid, PsionicComponent component, PsionicPowerUsedEvent args)
        {
            foreach (var entity in _lookup.GetEntitiesInRange(uid, 10f))
            {
                if (
                    HasComp<MetapsionicPowerComponent>(entity)
                    && entity != uid
                    && !(TryComp<PsionicInsulationComponent>(entity, out var insul) && !insul.Passthrough)
                )
                {
                    _popups.PopupEntity(
                        Loc.GetString("metapsionic-pulse-power", ("power", args.Power)),
                        entity,
                        entity,
                        PopupType.LargeCaution
                    );
                    args.Handled = true;
                    return;
                }
            }
        }

        private void OnInit(EntityUid uid, PsionicsDisabledComponent component, ComponentInit args)
        {
            SetPsionicsThroughEligibility(uid);
        }

        private void OnShutdown(EntityUid uid, PsionicsDisabledComponent component, ComponentShutdown args)
        {
            SetPsionicsThroughEligibility(uid);
        }

        private void OnMobStateChanged(EntityUid uid, PsionicComponent component, MobStateChangedEvent args)
        {
            SetPsionicsThroughEligibility(uid);
        }

        /// <summary>
        /// Checks whether the entity is eligible to use its psionic ability. This should be run after anything that could effect psionic eligibility.
        /// </summary>
        public void SetPsionicsThroughEligibility(EntityUid uid)
        {
            PsionicComponent? component = null;
            if (!Resolve(uid, ref component, false))
                return;

            // Update all psionic actions based on eligibility
            foreach (var action in component.Actions.Values)
            {
                if (action == null)
                    continue;

                _actions.TryGetActionData(action.Value, out var actionData);

                if (actionData == null)
                    continue;

                _actions.SetEnabled(actionData.Owner, IsEligibleForPsionics(uid));
            }
        }

        private bool IsEligibleForPsionics(EntityUid uid)
        {
            return !HasComp<PsionicInsulationComponent>(uid)
                && (!TryComp<MobStateComponent>(uid, out var mobstate) || mobstate.CurrentState == MobState.Alive);
        }

        public void LogPowerUsed(EntityUid uid, string power, int minGlimmer = 8, int maxGlimmer = 12)
        {
            _adminLogger.Add(
                Database.LogType.Psionics,
                Database.LogImpact.Medium,
                $"{ToPrettyString(uid):player} used {power}"
            );
            var ev = new PsionicPowerUsedEvent(uid, power);
            RaiseLocalEvent(uid, ev, false);

            _glimmerSystem.Glimmer += _robustRandom.Next(minGlimmer, maxGlimmer);
        }

        /// <summary>
        ///     Checks if a psionic can attempt to use their power.
        /// </summary>
        public bool OnAttemptPowerUse(EntityUid uid, string power)
        {
            if (!TryComp<PsionicComponent>(uid, out var psionicComponent))
                return false;

            if (HasComp<PsionicInsulationComponent>(uid))
                return false;

            if (HasComp<MindbrokenComponent>(uid))
                return false;

            return IsEligibleForPsionics(uid);
        }

        /// <summary>
        ///     Checks if a psionic can attempt to use their power with mana cost.
        /// </summary>
        public bool OnAttemptPowerUse(EntityUid uid, string power, float manaCost)
        {
            return OnAttemptPowerUse(uid, power);
        }

        /// <summary>
        ///     Checks if a psionic can attempt to use their power with mana cost and insulation check.
        /// </summary>
        public bool OnAttemptPowerUse(EntityUid uid, string power, float manaCost, bool checkInsulation)
        {
            if (!TryComp<PsionicComponent>(uid, out var psionicComponent))
                return false;

            if (checkInsulation && HasComp<PsionicInsulationComponent>(uid))
                return false;

            if (HasComp<MindbrokenComponent>(uid))
                return false;

            return IsEligibleForPsionics(uid);
        }

        /// <summary>
        ///     Gets the modified amplification for a psionic entity.
        /// </summary>
        public float ModifiedAmplification(EntityUid uid, PsionicComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return 1.0f;

            return component.CurrentAmplification;
        }

        /// <summary>
        ///     Gets the modified dampening for a psionic entity.
        /// </summary>
        public float ModifiedDampening(EntityUid uid, PsionicComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return 1.0f;

            return component.CurrentDampening;
        }

        /// <summary>
        ///     Validates if a psionic power can be used and handles the attempt.
        /// </summary>
        public bool OnAttemptPowerUse(EntityUid uid, string power, float? manaCost = null, bool doAfterCheck = true)
        {
            if (!TryComp<PsionicComponent>(uid, out var component))
                return false;

            // Check if already casting
            if (doAfterCheck && component.DoAfter != null)
            {
                _popups.PopupEntity(Loc.GetString(component.AlreadyCasting), uid, uid);
                return false;
            }

            // Check mana cost
            if (manaCost.HasValue && component.Mana < manaCost.Value)
            {
                _popups.PopupEntity(Loc.GetString(component.NoMana), uid, uid);
                return false;
            }

            // Deduct mana if cost specified
            if (manaCost.HasValue)
            {
                component.Mana -= manaCost.Value;
                component.Mana = Math.Max(0, component.Mana);
            }

            return true;
        }

        /// <summary>
        ///     Logs power usage for admin tracking.
        /// </summary>
        public void LogPowerUsed(EntityUid uid, string power)
        {
            _adminLogger.Add(
                Database.LogType.Psionics,
                Database.LogImpact.Low,
                $"{ToPrettyString(uid)} used psionic power {power}"
            );
        }
    }

    public sealed class PsionicPowerUsedEvent : HandledEntityEventArgs
    {
        public EntityUid User { get; }
        public string Power = string.Empty;

        public PsionicPowerUsedEvent(EntityUid user, string power)
        {
            User = user;
            Power = power;
        }
    }

    [Serializable]
    [NetSerializable]
    public sealed class PsionicsChangedEvent : EntityEventArgs
    {
        public readonly NetEntity Euid;

        public PsionicsChangedEvent(NetEntity euid)
        {
            Euid = euid;
        }
    }
}
