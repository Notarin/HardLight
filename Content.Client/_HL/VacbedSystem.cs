using Content.Shared._HL.Vacbed;
using Content.Shared.Verbs;
using Robust.Client.GameObjects;

namespace Content.Client._HL.Vacbed;

public sealed class VacbedSystem : SharedVacbedSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VacbedComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<VacbedComponent, GetVerbsEvent<AlternativeVerb>>(AddAlternativeVerbs);
        SubscribeLocalEvent<VacbedComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, VacbedComponent component, ref AppearanceChangeEvent args)
    {
        //todo figure out appearances and finish this

        if (args.Sprite == null) { return; }

        if (!_appearance.TryGetData<bool>(uid, VacbedComponent.VacbedVisuals.ContainsEntity, out var isEmpty, args.Component))
        {
            return;
        }

        _sprite.LayerSetRsiState((uid, args.Sprite), VacbedVisualLayers.Base, isEmpty ? "vacbed" : "vacbed_filled");
    }
}

//i'm genuinely too lazy to check if there's an easier way to handle visuals than this
//it shouldn't need a layer to work but SpriteComponent seems to assume it exists so here we are
public enum VacbedVisualLayers : byte
{
    Base,
}
