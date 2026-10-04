using System.Numerics;
using Content.Shared._HL.Whisp.Components;
using Content.Shared._HL.Whisp.Events;
using Content.Shared.Examine;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._HL.Whisp;

/// <summary>
/// Represents a system responsible for managing "Whisps", a secret entity.
/// This system handles the spawning, updating, and related logic for Whisps.
/// <br/><br/>
/// When the whisp spawns, it starts at a set distance from the target.
/// It then closes in on the target until it reaches the set distance.
/// Currently, at that point, the whisp will despawn.
/// </summary>
public sealed class SharedWhispSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPvsOverrideSystem _pvs = default!;

    private static readonly EntProtoId WhispPrototypeId = "MobWhisp";

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        // Ensures whisp position updates happen after transform system updates to prevent it from lagging behind by one frame.
        UpdatesAfter.Add(typeof(SharedTransformSystem));

        SubscribeLocalEvent<SpawnWhispEvent>(OnSpawnWhispEvent);
        SubscribeLocalEvent<WhispComponent, ExamineAttemptEvent>(OnExamineAttempt);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    /// <summary>
    /// Handles <see cref="PlayerAttachedEvent"/>.
    /// <br/>
    /// Overrides the PVS of whisps that are attached to a player.<br/>
    /// Ensures that the player is sent the whisp on its spawn, regardless of distance.
    /// </summary>
    /// <param name="args">See <see cref="PlayerAttachedEvent"/>.</param>
    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<WhispComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Target == args.Entity)
                _pvs.AddSessionOverride(uid, args.Player);
        }
    }

    /// <remarks>
    /// I really don't think this is necessary.
    /// I am like fairly convinced that you can't examine it regardless.
    /// </remarks>
    private static void OnExamineAttempt(EntityUid uid, WhispComponent comp, ExamineAttemptEvent args)
    {
        if (args.Examiner != comp.Target)
            args.Cancel();
    }

    /// <summary>
    /// Handles <see cref="SpawnWhispEvent"/>. See <see cref="SpawnWhisp"/>.
    /// </summary>
    /// <remarks>
    /// Needed to provide the interface necessary to subscribe to the spawn event.
    /// <br/>
    /// Should only fire on the server.
    /// </remarks>
    private void OnSpawnWhispEvent(SpawnWhispEvent ev) =>
        SpawnWhisp(ev.Target, ev.Distance, ev.Speed);

    /// <summary>
    /// Spawns a whisp at a given distance from the target.
    /// Preps info to begin tracking the target per frame.
    /// </summary>
    /// <returns>The Whisp entity, or null if the target is invalid or already terminated.</returns>
    public EntityUid? SpawnWhisp(EntityUid target, float distance = 20f, float speed = 0.5f)
    {
        var notServer = !_net.IsServer; // Client shouldn't ever be firing this anyway.
        var entityIsInvalid = Deleted(target) || Terminating(target);
        if (notServer || entityIsInvalid)
            return null;

        // Is on valid map with a proper location
        if (!TryComp(target, out TransformComponent? targetXform) ||
            targetXform.MapUid is not { } mapUid)
            return null;

        var targetParent = targetXform.GridUid ?? mapUid;
        var targetPos = _transform.GetWorldPosition(targetXform);

        // Randomize the direction it comes in from.
        var randomAngle = _random.NextFloat(0f, MathF.Tau);
        var direction = new Vector2(MathF.Cos(randomAngle), MathF.Sin(randomAngle));
        var spawnWorldPos = targetPos + direction * distance;

        // Get the inverse world matrix of the target's parent (the grid or map).
        // This is needed to convert world coordinates back into the coordinates relative to the parent.
        var invParent = _transform.GetInvWorldMatrix(targetParent);
        var spawnLocalPos = Vector2.Transform(spawnWorldPos, invParent);
        var spawnCoords = new EntityCoordinates(targetParent, spawnLocalPos); // Finally

        var whisp = Spawn(WhispPrototypeId, spawnCoords);
        var whispComp = EnsureComp<WhispComponent>(whisp);
        whispComp.Target = target;
        whispComp.InitialDistance = MathF.Max(0.01f, distance);
        whispComp.CurrentDistance = whispComp.InitialDistance;
        whispComp.ClosingSpeed = MathF.Max(0.01f, speed);
        whispComp.LastDirection = direction;
        // Since we're instantiating this ourselves, we've dirtied the state, so now that we're done, call dirty.
        Dirty(whisp, whispComp);

        if (TryComp<ActorComponent>(target, out var actor))
            _pvs.AddSessionOverride(whisp, actor.PlayerSession);

        return whisp;
    }

    /// <inheritdoc/>
    /// <summary>
    /// <br/>
    /// Server-side iterative updating whisp position.<br/>
    /// See <see cref="FrameUpdate"/> for client-side prediction.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Server-side only. Authoritative.
        if (!_net.IsServer)
            return;

        var allWhispsQuery = EntityQueryEnumerator<WhispComponent, TransformComponent>();
        while (allWhispsQuery.MoveNext(out var uid, out var whispComp, out var whispXform))
        {
            UpdateWhisp(uid, whispComp, whispXform, frameTime, isServer: true);
        }
    }

    /// <inheritdoc/>
    /// <summary>
    /// <br/>
    /// Client-side iterative predicting whisp position.<br/>
    /// See <see cref="Update"/> for server-side calculation.
    /// </summary>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Client-side only. Predictive.
        if (!_net.IsClient)
            return;

        var allWhispsQuery = EntityQueryEnumerator<WhispComponent, TransformComponent>();
        while (allWhispsQuery.MoveNext(out var uid, out var whispComp, out var whispXform))
        {
            UpdateWhisp(uid, whispComp, whispXform, frameTime, isServer: false);
        }
    }

    /// <summary>
    /// Processes a single tick of movement+logic for an active Whisp.
    /// Manages closing distance to the target, firing arrival event, and client-side prediction.
    /// </summary>
    /// <param name="uid">The whisp.</param>
    /// <param name="whispComp">The whisp's component.</param>
    /// <param name="whispXform">The whisp's transform component.</param>
    /// <param name="frameTime">Time elapsed since the last frame.</param>
    /// <param name="isServer">Whether this update is happening on the authoritative model, the server.</param>
    private void UpdateWhisp(EntityUid uid, WhispComponent whispComp, TransformComponent whispXform, float frameTime, bool isServer)
    {
        #region Target Validation

        // If the target is invalid or incorporeal, nuke the whisp; it's not necessary anymore.
        // If the whisp somehow found itself in nullspace, also nuke it.
        if (whispComp.Target is not { Valid: true } target ||
            Deleted(target) ||
            Terminating(target) ||
            !TryComp(target, out TransformComponent? targetXform) ||
            targetXform.MapUid is not { } targetMapUid ||
            !whispXform.ParentUid.IsValid())
        {
            if (isServer)
                QueueDel(uid);
            return;
        }

        #endregion

        #region Lockstep Parenting & Client/Server Cooperation

        var targetParent = targetXform.GridUid ?? targetMapUid;

        if (isServer)
        {
            // Enforce the whisp shares the same parent (grid or map) as its target.
            // Not letting the movement system do it itself.
            // We need its referential position to be lockstep with the target.
            if (whispXform.ParentUid != targetParent)
                _transform.SetParent(uid, whispXform, targetParent);
        }
        else
        {
            // On client, if maps don't match yet, wait for transform parent sync
            if (whispXform.MapUid != targetMapUid)
            {
                whispComp.PredictedPosition = null;
                return;
            }

            if (whispXform.ParentUid != targetParent && targetParent.IsValid())
            {
                _transform.SetParent(uid, whispXform, targetParent);
            }
        }

        #endregion

        #region Movement Calculation

        // Reduce stalking distance
        whispComp.CurrentDistance = MathF.Max(whispComp.ReachDistance, whispComp.CurrentDistance - whispComp.ClosingSpeed * frameTime);

        // Find vector relationship between target and whisp
        var targetPos = _transform.GetWorldPosition(targetXform);

        // Use prediction on the client to ensure responsive movement, fallback to real transform on server.
        var whispPos = !isServer && whispComp.PredictedPosition.HasValue
            ? whispComp.PredictedPosition.Value
            : _transform.GetWorldPosition(whispXform);

        var difference = whispPos - targetPos;
        var distanceToTarget = difference.Length();

        // Calculate the direction pointing FROM the target TO the whisp.
        // Fallback to the last known direction if they are directly on top of each other to prevent division by zero.
        // Guess how I learned that's necessary.
        var direction = distanceToTarget > 0.0001f
            ? difference / distanceToTarget
            : whispComp.LastDirection;

        whispComp.LastDirection = direction;

        // Calculate the exact world position we want the whisp to be at this nonce.
        var desiredWorldPos = targetPos + direction * whispComp.CurrentDistance;

        if (!isServer) // Client-side prediction. Saving the predicted position for the next frame.
            whispComp.PredictedPosition = desiredWorldPos;

        #endregion

        #region Apply Calculated Position

        // Convert the world position back to local coordinates relative to the whisp's parent.
        var parentInvMatrix = _transform.GetInvWorldMatrix(whispXform.ParentUid);
        var desiredLocalPos = Vector2.Transform(desiredWorldPos, parentInvMatrix);

        // We FINALLY snap the whisp to the new position.
        // Why are we not using lerping?
        // Well because the initial attempt with lerping and no prediction makes the whisp VERY laggy to respond to movement.
        // I need it to respond INSTANTLY. So we're handling its position per-frame on the client, thus making lerping unneccessary.
        _transform.SetLocalPositionNoLerp(uid, desiredLocalPos, whispXform);
        whispXform.ActivelyLerping = false;

        #endregion

        #region Arrival/Catching Logic

        // Check if the whisp physically caught the target.
        // It must physically reach the player in world space AND have closed the stalking distance.
        var actualWorldPos = _transform.GetWorldPosition(whispXform);
        var actualDistance = (actualWorldPos - targetPos).Length();

        if (!(actualDistance <= whispComp.ReachDistance + 0.15f) ||
            !(whispComp.CurrentDistance <= whispComp.ReachDistance + 0.05f) ||
            !isServer)
            return;
        // The whisp has arrived. Fire off event and throw the whisp in the woodchipper.
        var reachedEv = new WhispReachedTargetEvent(uid, target);
        RaiseLocalEvent(target, reachedEv); // Fire the event on the target.
        QueueDel(uid);

        #endregion
    }
}
