using System.Numerics;
using Content.Client.Administration.Managers;
using Content.Client.Resources;
using Content.Shared._HL.Whisp.Components;
using Content.Shared.Administration;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._HL.Whisp;

/// <summary>
/// An overlay that displays jittery text around approaching whisps.
/// Cognito hazard inspired.
/// </summary>
public sealed class WhispTextOverlay : Overlay
{
    private const string FontPath = "/Fonts/NotoSans/NotoSans-Bold.ttf";
    private const int FontSize = 15;
    private const int SlotCount = 8;

    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IClientAdminManager _adminManager = default!;
    private readonly SharedTransformSystem _transform;

    /// <inheritdoc/>
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly Font _font;

    /// <summary>
    /// Represents an active floating phrase slot floating around a whisp.
    /// </summary>
    private struct TextSlot
    {
        /// <summary>The phrase to show.</summary>
        public string Text;

        /// <summary>Offset. World coords. Relative to the whisps centerpoint.</summary>
        public Vector2 Offset;

        /// <summary>How long the text lasts before it despawns (to be replaced).</summary>
        public float Lifetime;

        /// <summary>Elapsed time since text spawn (seconds).</summary>
        public float Age;

        /// <summary>Text render scale.</summary>
        public float Scale;

        /// <summary>Text color (red or white).</summary>
        public Color Color;
    }

    private readonly TextSlot[] _slots = new TextSlot[SlotCount];

    /// <summary>
    /// Pool of paranoid, panicky phrases flashed on screen.
    /// </summary>
    private static readonly string[] Phrases =
    [
        "DONT LOOK",
        "RUN! RUN!!!",
        "DO NOT LOOK",
        "LOOK AWAY!",
        "LOOK AWAY",
        "CLOSE YOUR EYES",
        "STOP LOOKING",
        "RUN",
        "AVOID",
        "DONT LOOK AT IT",
        "COGNITO HAZARD!",
        "DANGER!",
        "DO NOT LOOK AT",
    ];

    /// <inheritdoc cref="WhispTextOverlay"/>
    public WhispTextOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entMan.System<SharedTransformSystem>();
        var resourceCache = IoCManager.Resolve<IResourceCache>();
        _font = resourceCache.GetFont(FontPath, FontSize);

        // Pre-fire slots with randomized ages so phrases start staggered rather than popping in simultaneously
        for (var i = 0; i < _slots.Length; i++)
        {
            RespawnSlot(ref _slots[i]);
            _slots[i].Age = _random.NextFloat(0f, _slots[i].Lifetime);
        }
    }

    /// <summary>
    /// Spawns text in a slot with a newly randomized phrase, lifetime, scale, color, and polar offset.
    /// </summary>
    /// <param name="slot">The slot to reinitialize.</param>
    private void RespawnSlot(ref TextSlot slot)
    {
        slot.Text = Phrases[_random.Next(Phrases.Length)];

        // Random direction and distance
        var angle = _random.NextFloat(0f, MathF.Tau);
        var dist = _random.NextFloat(0f, 1.3f);
        slot.Offset = new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);

        // Randomize scale and lifetime
        slot.Lifetime = _random.NextFloat(0.25f, 0.65f);
        slot.Age = 0f;
        slot.Scale = _random.NextFloat(0.85f, 1.25f);

        // 33% chance of red text
        slot.Color = _random.Next(3) == 0 ? Color.FromHex("#ff2222") : Color.White;
    }

    /// <inheritdoc/>
    /// <summary>
    /// <br/>
    /// Every frame advances all the slots' ages, and cycles outlived slots.
    /// </summary>
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Advance slot ages and refresh expired phrases/slots
        for (var i = 0; i < _slots.Length; i++)
        {
            _slots[i].Age += args.DeltaSeconds;
            if (_slots[i].Age >= _slots[i].Lifetime)
            {
                RespawnSlot(ref _slots[i]);
            }
        }
    }

    /// <inheritdoc/>
    /// <summary>
    /// <br/>
    /// Only draws the overlay if the player is looking through their own eyes,
    /// or if the user is an admin with the Fun flag.
    /// </summary>
    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_adminManager.HasFlag(AdminFlags.Fun))
            return true;

        // Only draw if viewing through the local player's own eyes
        if (!_entMan.TryGetComponent(_player.LocalEntity, out EyeComponent? eyeComp))
            return false;

        return args.Viewport.Eye == eyeComp.Eye;
    }

    /// <inheritdoc/>
    /// <summary>
    /// This renders the <see cref="WhispTextOverlay"/>.
    /// </summary>
    protected override void Draw(in OverlayDrawArgs args)
    {
        // Ensure valid viewport
        if (args.ViewportControl == null)
            return;

        var localPlayer = _player.LocalEntity;
        var isAdmin = _adminManager.HasFlag(AdminFlags.Fun);

        var handle = args.ScreenHandle;
        var whispsQuery = _entMan.EntityQueryEnumerator<WhispComponent, TransformComponent>();

        while (whispsQuery.MoveNext(out _, out var whispComp, out var whispXform))
        {
            var isTarget = localPlayer != null && whispComp.Target == localPlayer;
            if (!isTarget && !isAdmin)
                continue;

            // We're taking advantage of the existing client-side prediction to get the text just as snappy as the whisp.
            var basePos = whispComp.PredictedPosition ?? _transform.GetWorldPosition(whispXform);

            for (var i = 0; i < _slots.Length; i++)
            {
                ref var slot = ref _slots[i];
                if (string.IsNullOrEmpty(slot.Text))
                    continue;

                var worldPos = basePos + slot.Offset;
                var screenPos = args.ViewportControl.WorldToScreen(worldPos);

                // Jitter offset to be extra startling.
                var jitter = new Vector2(
                    _random.NextFloat(-4f, 4f),
                    _random.NextFloat(-4f, 4f)
                );

                var drawPos = screenPos + jitter;
                var dim = handle.GetDimensions(_font, slot.Text, slot.Scale);
                var origin = drawPos - dim / 2f;

                // Touchups, outline, shadow.
                handle.DrawString(_font, origin + new Vector2(1.5f, 1.5f), slot.Text, slot.Scale, Color.Black.WithAlpha(0.9f));
                handle.DrawString(_font, origin, slot.Text, slot.Scale, slot.Color);
            }
        }
    }
}
