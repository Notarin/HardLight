using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._HL.Whisp;

/// <summary>
/// A post-processing overlay that renders TV-like static over the player's view.
/// The intensity of this overlay is driven by <see cref="WhispOverlaySystem"/>.
/// </summary>
public sealed class WhispStaticOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;

    /// <inheritdoc/>
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    /// <inheritdoc/>
    public override bool RequestScreenTexture => true;

    /// <summary>
    /// How intense the static effect is, typically mapped from 0.0 to 1.0.
    /// </summary>
    public float Strength = 0.0f;

    private readonly ShaderInstance _shader;

    private static readonly ProtoId<ShaderPrototype> ShaderId = "WhispStatic";

    /// <inheritdoc cref="WhispStaticOverlay"/>
    public WhispStaticOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _protoMan.Index(ShaderId).InstanceUnique();
    }

    /// <inheritdoc/>
    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        // We only want to draw this effect if we are looking through the local player's actual eyes.
        // If they are looking through a camera console, drone, or mapping tool, fuck the static!
        if (!_entMan.TryGetComponent(_player.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        // No point in telling the GPU to draw if the effect is currently invisible.
        return Strength > 0.0f;
    }

    /// <inheritdoc/>
    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;

        // Feed our texture and strength var into the shader
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("strength", Strength);

        // Bob Ross all over the screen
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);

        // Always clean up your brushes when you're done painting
        handle.UseShader(null);
    }
}
