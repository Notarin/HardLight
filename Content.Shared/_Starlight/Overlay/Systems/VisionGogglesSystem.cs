using Content.Shared.Clothing;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Network;

namespace Content.Shared._Starlight.Overlay;

/// <summary>
/// Applies a toggled pair of vision goggles to its wearer. The vision components
/// belong on the wearer because the client overlays are attached to the player's eye.
/// </summary>
public sealed class VisionGogglesSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VisionGogglesComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<VisionGogglesComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<VisionGogglesComponent, PulseThermalVisionGogglesEvent>(OnThermalPulse);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsServer)
            TickPulses(frameTime);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_net.IsClient)
            TickPulses(frameTime);
    }

    private void TickPulses(float frameTime)
    {
        var query = EntityQueryEnumerator<VisionGogglesComponent>();
        while (query.MoveNext(out var uid, out var goggles))
        {
            if (goggles.PulseWearer is not { } wearer)
                continue;

            if (goggles.PulseDuration <= 0f || goggles.PulseAccumulator >= goggles.PulseDuration)
            {
                EndPulse((uid, goggles), wearer);
                continue;
            }

            goggles.PulseAccumulator += frameTime;
            goggles.PulseRemaining = MathF.Max(0f, goggles.PulseDuration - goggles.PulseAccumulator);
            if (goggles.PulseAccumulator >= goggles.PulseDuration)
                EndPulse((uid, goggles), wearer);
        }
    }

    private void OnToggled(Entity<VisionGogglesComponent> ent, ref ItemToggledEvent args)
    {
        if (args.User is not { } wearer)
            return;

        if (args.Activated)
            GrantVision(ent, wearer);
        else
            RemoveVision(ent, wearer);
    }

    private void OnUnequipped(Entity<VisionGogglesComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        RemoveVision(ent, args.Wearer);
        ent.Comp.PulseRemaining = 0f;
        ent.Comp.PulseWearer = null;
        Dirty(ent);
    }

    private void OnThermalPulse(Entity<VisionGogglesComponent> ent, ref PulseThermalVisionGogglesEvent args)
    {
        if (ent.Comp.Vision != VisionGogglesType.Thermal || ent.Comp.PulseDuration <= 0f)
            return;

        ent.Comp.GrantedVision = true;
        ent.Comp.PulseAccumulator = 0f;
        ent.Comp.PulseRemaining = ent.Comp.PulseDuration;
        ent.Comp.PulseWearer = args.Performer;
        Dirty(ent);
        args.Handled = true;
    }

    private void GrantVision(Entity<VisionGogglesComponent> ent, EntityUid wearer)
    {
        switch (ent.Comp.Vision)
        {
            case VisionGogglesType.Night:
                if (HasComp<NightVisionComponent>(wearer))
                    return;

                AddComp(wearer, new NightVisionComponent { Active = true });
                break;
            case VisionGogglesType.Thermal:
                break;
        }

        ent.Comp.GrantedVision = true;
        Dirty(ent);
    }

    private void RemoveVision(Entity<VisionGogglesComponent> ent, EntityUid wearer)
    {
        if (!ent.Comp.GrantedVision)
            return;

        switch (ent.Comp.Vision)
        {
            case VisionGogglesType.Night:
                RemComp<NightVisionComponent>(wearer);
                break;
            case VisionGogglesType.Thermal:
                break;
        }

        ent.Comp.GrantedVision = false;
        Dirty(ent);
    }

    private void EndPulse(Entity<VisionGogglesComponent> ent, EntityUid wearer)
    {
        RemoveVision(ent, wearer);
        ent.Comp.PulseRemaining = 0f;
        ent.Comp.PulseWearer = null;
        Dirty(ent);
    }
}
