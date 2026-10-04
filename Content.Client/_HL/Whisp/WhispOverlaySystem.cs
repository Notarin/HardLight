using Content.Shared._HL.Whisp.Components;
using Robust.Client.GameObjects;
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
    [Dependency] private readonly SpriteSystem _sprite = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    /// <summary>
    /// Handles <see cref="LocalPlayerDetachedEvent"/>.
    /// <br/>
    /// Hides whisps when the player disconnects.
    /// </summary>
    /// <param name="args">See <see cref="LocalPlayerDetachedEvent"/>.</param>
    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        HideAllWhisps();
    }

    /// <inheritdoc/>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // If the client has no entity/body (e.g., in the lobby), we shouldn't have any effects.
        if (_player.LocalEntity is not { Valid: true } localPlayer)
        {
            HideAllWhisps();
            return;
        }

        var query = EntityQueryEnumerator<WhispComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var whispComp, out var sprite))
        {
            var isTarget = whispComp.Target == localPlayer;
            if (sprite.Visible != isTarget)
                _sprite.SetVisible((uid, sprite), isTarget);
        }
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
