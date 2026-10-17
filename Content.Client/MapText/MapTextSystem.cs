using Content.Shared.MapText;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.MapText;

/// <inheritdoc/>
public sealed class MapTextSystem : SharedMapTextSystem
{
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private MapTextOverlay _overlay = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MapTextComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<MapTextComponent, ComponentHandleState>(HandleCompState);

        _overlay = new MapTextOverlay(_configManager, EntityManager, _uiManager, _transform, _resourceCache, _prototypeManager);
        _overlayManager.AddOverlay(_overlay);

        // TODO move font prototype to robust.shared, then use ProtoId<FontPrototype>
        DebugTools.Assert(_prototypeManager.HasIndex<FontPrototype>(SharedMapTextComponent.DefaultFont));
    }

    private void OnComponentStartup(Entity<MapTextComponent> ent, ref ComponentStartup args)
    {
        CacheText(ent.Comp);
        // TODO move font prototype to robust.shared, then use ProtoId<FontPrototype>
        DebugTools.Assert(_prototypeManager.HasIndex<FontPrototype>(ent.Comp.FontId));
    }

    private void HandleCompState(Entity<MapTextComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not MapTextComponentState state)
            return;

        ent.Comp.Text = state.Text;
        ent.Comp.LocText = state.LocText;
        ent.Comp.Color = state.Color;
        ent.Comp.FontId = state.FontId;
        ent.Comp.FontSize = state.FontSize;
        ent.Comp.Offset = state.Offset;

        CacheText(ent.Comp);
    }

    private const string DefaultFontPath = "/Fonts/NotoSans/NotoSans-Regular.ttf";

    private static bool TryGetFontPath(string fontId, out string path)
    {
        path = fontId switch
        {
            "Default" => DefaultFontPath,
            "DefaultItalic" => "/Fonts/NotoSans/NotoSans-Italic.ttf",
            "DefaultBold" => "/Fonts/NotoSans/NotoSans-Bold.ttf",
            "DefaultBoldItalic" => "/Fonts/NotoSans/NotoSans-BoldItalic.ttf",
            "NotoSansDisplay" => "/Fonts/NotoSansDisplay/NotoSansDisplay-Regular.ttf",
            "NotoSansDisplayItalic" => "/Fonts/NotoSansDisplay/NotoSansDisplay-Italic.ttf",
            "NotoSansDisplayBold" => "/Fonts/NotoSansDisplay/NotoSansDisplay-Bold.ttf",
            "NotoSansDisplayBoldItalic" => "/Fonts/NotoSansDisplay/NotoSansDisplay-BoldItalic.ttf",
            "BoxRound" => "/Fonts/Boxfont-round/Boxfont Round.ttf",
            "AnimalSilence" => "/Fonts/Animal Silence.otf",
            "Monospace" => "/EngineFonts/NotoSans/NotoSansMono-Regular.ttf",
            "Emoji" => "/Fonts/NotoEmoji.ttf",
            "OnlyYou" => "/Fonts/Only_You.otf",
            "Chinese" => "/Fonts/NotoSans/NotoSansSC-Regular.ttf",
            "Sriracha" => "/Fonts/Sriracha/Sriracha.ttf",
            "GrenzeGotisch" => "/Fonts/Grenze_Gotisch/GrenzeGotisch.ttf",
            "RubikDirt" => "/Fonts/Rubik_Dirt/RubikDirt.ttf",
            "Lymphatic" => "/Fonts/_Starlight/Lymphatic.ttf",
            "RubikBubbles" => "/Fonts/_Starlight/RubikBubbles.ttf",
            "Copperplate" => "/Fonts/_Starlight/Copperplate.otf",
            "Mangat" => "/Fonts/_Starlight/Mangat.ttf",
            "Scratch" => "/Fonts/_Starlight/Scratch.ttf",
            "Octatrax" => "/Fonts/_Starlight/Octatrax.ttf",
            "Draconic" => "/Fonts/_Starlight/Draconic-modern.otf",
            _ => string.Empty,
        };

        return !string.IsNullOrEmpty(path);
    }

    private void CacheText(MapTextComponent component)
    {
        component.CachedFont = null;

        component.CachedText = string.IsNullOrWhiteSpace(component.Text)
            ? Loc.GetString(component.LocText)
            : component.Text;

        if (!_prototypeManager.HasIndex<FontPrototype>(component.FontId) ||
            !TryGetFontPath(component.FontId, out var fontPath) ||
            !_resourceCache.TryGetResource<FontResource>(fontPath, out var fontResource))
        {
            component.CachedText = Loc.GetString("map-text-font-error");
            component.Color = Color.Red;

            if (_resourceCache.TryGetResource<FontResource>(DefaultFontPath, out var defaultFont))
                component.CachedFont = new VectorFont(defaultFont, 14);
            return;
        }

        component.CachedFont = new VectorFont(fontResource, component.FontSize);
    }
}
