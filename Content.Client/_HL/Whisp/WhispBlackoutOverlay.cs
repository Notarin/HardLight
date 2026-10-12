using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._HL.Whisp;

/// <summary>
/// An overlay triggered when a whisp catches a player.
/// Freezes the current screen viewport into a snapshot and applies GPU distortion.
/// And blackout effect that decays over <see cref="Duration"/> seconds.
/// </summary>
public sealed class WhispBlackoutOverlay : Overlay
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;

    /// <inheritdoc/>
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    /// <inheritdoc/>
    public override bool RequestScreenTexture => !_hasCaptured;

    /// <summary>
    /// Total duration of the blackout effect in seconds.
    /// </summary>
    public const float Duration = 10.0f;

    /// <summary>
    /// Time remaining for the current effect.
    /// </summary>
    private float _timeRemaining;

    private readonly ShaderInstance _shader;
    private static readonly ProtoId<ShaderPrototype> ShaderId = "WhispGpuDeath";

    private bool _hasCaptured;
    private IRenderTexture? _targetA;
    private IRenderTexture? _targetB;
    private bool _flop;

    /// <inheritdoc cref="WhispBlackoutOverlay"/>
    public WhispBlackoutOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _protoMan.Index(ShaderId).InstanceUnique();
        ZIndex = 100;
    }

    /// <summary>
    /// Triggers the blackout effect, resets timer.
    /// </summary>
    public void Trigger()
    {
        Reset();
        _timeRemaining = Duration;
    }

    /// <summary>
    /// Returns whether the blackout overlay is currently active and degrading.
    /// </summary>
    public bool IsActive => _timeRemaining > 0f;

    /// <summary>
    /// Resets the overlay state, clearing render targets and zeroing timers.
    /// </summary>
    public void Reset()
    {
        _timeRemaining = 0f;
        _hasCaptured = false;
        _flop = false;
        _targetA?.Dispose();
        _targetA = null;
        _targetB?.Dispose();
        _targetB = null;
    }

    /// <inheritdoc/>
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!(_timeRemaining > 0f))
            return;
        _timeRemaining -= args.DeltaSeconds;
        if (_timeRemaining <= 0f)
            Reset();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Determines whether the overlay should be drawn for the current viewport.
    /// This ensures the blackout effect only renders for the local player's eye and not other viewports
    /// (e.g., security cameras, etc.).
    /// </remarks>
    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_timeRemaining <= 0f)
            return false;

        if (!_entMan.TryGetComponent(_player.LocalEntity, out EyeComponent? eyeComp))
            return false;

        return args.Viewport.Eye == eyeComp.Eye;
    }

    /// <inheritdoc/>
    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_timeRemaining <= 0f)
            return;

        var screenHandle = args.ScreenHandle;
        var size = args.Viewport.Size;
        if (size.X <= 0 || size.Y <= 0)
            return;

        // On shader initialization, grab the screen texture and save that
        if (!_hasCaptured)
        {
            if (ScreenTexture == null)
                return;

            _targetA?.Dispose();
            _targetB?.Dispose();
            _targetA = _clyde.CreateRenderTarget(size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "whisp-freeze-a");
            _targetB = _clyde.CreateRenderTarget(size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "whisp-freeze-b");

            var screenTex = ScreenTexture;
            screenHandle.RenderInRenderTarget(_targetA,
                () =>
            {
                screenHandle.SetTransform(Matrix3x2.Identity);
                screenHandle.DrawTextureRect(screenTex, new UIBox2(0, 0, size.X, size.Y));
            },
                Color.Black);

            _hasCaptured = true;
            _flop = true;

            screenHandle.SetTransform(Matrix3x2.Identity);
            screenHandle.DrawTextureRect(_targetA.Texture, (UIBox2)args.ViewportBounds);
            return;
        }

        if (_targetA == null || _targetB == null)
            return;

        var input = _flop ? _targetA : _targetB;
        var output = _flop ? _targetB : _targetA;

        var progress = Math.Clamp(1.0f - _timeRemaining / Duration, 0.0f, 1.0f);

        // Use the snapshot every frame and write back to it, progressively degrading it
        screenHandle.RenderInRenderTarget(output,
            () =>
        {
            screenHandle.SetTransform(Matrix3x2.Identity);
            _shader.SetParameter("SCREEN_TEXTURE", input.Texture);
            _shader.SetParameter("progress", progress);
            screenHandle.UseShader(_shader);
            screenHandle.DrawTextureRect(input.Texture, new UIBox2(0, 0, size.X, size.Y));
            screenHandle.UseShader(null);
        },
            Color.Black);

        // Draw the output to screen
        screenHandle.SetTransform(Matrix3x2.Identity);
        screenHandle.DrawTextureRect(output.Texture, args.ViewportBounds);

        // flipflop for the next frame
        _flop = !_flop;
    }

    /// <inheritdoc/>
    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();
        Reset();
    }
}
