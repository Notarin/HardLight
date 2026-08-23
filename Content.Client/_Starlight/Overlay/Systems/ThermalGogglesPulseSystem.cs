using Content.Shared.Eye.Blinding.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._Starlight.Overlay;

public sealed class ThermalGogglesPulseSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private ThermalGogglesPulseOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new ThermalGogglesPulseOverlay();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        VisionGogglesComponent? active = null;
        var query = EntityQueryEnumerator<VisionGogglesComponent>();
        while (query.MoveNext(out _, out var goggles))
        {
            if (goggles.Vision != VisionGogglesType.Thermal ||
                goggles.PulseWearer != _player.LocalEntity ||
                goggles.PulseDuration <= 0f ||
                goggles.PulseAccumulator >= goggles.PulseDuration)
                continue;

            active = goggles;
            break;
        }

        _overlay.Component = active;
        if (active != null)
        {
            if (!_overlayManager.HasOverlay<ThermalGogglesPulseOverlay>())
                _overlayManager.AddOverlay(_overlay);
            return;
        }

        _overlayManager.RemoveOverlay(_overlay);
        _overlay.ResetLight();
    }
}
