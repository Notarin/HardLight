using Content.Shared._HL.Whisp.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._HL.Whisp;

/// <summary>
/// Handles client-side visibility and effects for whisps.
/// </summary>
/// <remarks>
/// Why an overlay system? Because this fits in with upcoming changes which are very much several overlays.
/// </remarks>
public sealed class WhispOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private WhispStaticOverlay _overlay = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _overlay = new WhispStaticOverlay();

        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    /// <inheritdoc/>
    public override void Shutdown()
    {
        base.Shutdown();
        RemoveOverlay();
    }

    /// <summary>
    /// Handles <see cref="LocalPlayerDetachedEvent" />.
    /// <br/>
    /// Stops the static overlay and hides whisps.
    /// </summary>
    /// <param name="args">See <see cref="LocalPlayerDetachedEvent"/>.</param>
    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        HideAllWhisps();
        RemoveOverlay();
    }

    /// <inheritdoc/>
    /// <summary>
    /// Evaluates the distance of all Whisps targeting the local player.
    /// Applies and scales the static overlay based on whichever Whisp is closest.
    /// </summary>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // If the client has no entity/body (e.g., in the lobby), we shouldn't have any effects.
        if (_player.LocalEntity is not { Valid: true } localPlayer)
        {
            HideAllWhisps();
            RemoveOverlay();
            return;
        }

        var maxStrength = 0f;
        var isTargetedByWhisp = false;

        var query = EntityQueryEnumerator<WhispComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var whispComp, out var sprite))
        {
            var isTarget = whispComp.Target == localPlayer;
            if (sprite.Visible != isTarget)
                _sprite.SetVisible((uid, sprite), isTarget);

            if (!isTarget)
                continue;

            isTargetedByWhisp = true;

            // Clamped to 0.01f to prevent division by zero in the next step. (sounds familiar)
            var totalTravelDistance = MathF.Max(0.01f, whispComp.InitialDistance - whispComp.ReachDistance);

            var distanceCovered = whispComp.InitialDistance - whispComp.CurrentDistance;

            var progress = Math.Clamp(distanceCovered / totalTravelDistance, 0f, 1f);

            // If multiple whisps are targeting the player, we want the overlay to match the closest one.
            // P.S. How the fuck did you get multiple of these things after you???
            if (progress > maxStrength)
                maxStrength = progress;
        }

        if (!isTargetedByWhisp)
        {
            RemoveOverlay();
            return;
        }
        // We know there are whisp(s) after us here.
        EnsureOverlay();
        _overlay.Strength = maxStrength;
    }

    /// <summary>
    /// Safely adds the overlay to the overlay manager if it isn't already active.
    /// </summary>
    private void EnsureOverlay()
    {
        if (!_overlayMan.HasOverlay<WhispStaticOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    /// <summary>
    /// Safely removes the overlay and resets its strength to zero.
    /// </summary>
    /// <remarks>
    /// Prroooobably don't need to clean up, but it can't hurt.
    /// </remarks>
    private void RemoveOverlay()
    {
        if (!_overlayMan.HasOverlay<WhispStaticOverlay>())
            return;

        // Scrubadubdub
        _overlay.Strength = 0f;
        _overlayMan.RemoveOverlay(_overlay);
    }

    /// <summary>
    /// Clears the visibility of all whisps.
    /// Kinda for outliers. Like when the player isn't a character, and this somehow slipped through all the way to here,
    /// or if the player logged out, etc.
    /// </summary>
    private void HideAllWhisps()
    {
        var whispsQuery = EntityQueryEnumerator<WhispComponent, SpriteComponent>();
        while (whispsQuery.MoveNext(out var whisp, out _, out var whispSprite))
        {
            if (whispSprite.Visible)
                _sprite.SetVisible((whisp, whispSprite), false);
        }
    }
}
