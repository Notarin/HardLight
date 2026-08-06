using Content.Client._Starlight.RCD;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
// Starlight Start
using Content.Shared.Input;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
// Starlight End

namespace Content.Client.RCD;

public sealed class RCDConstructionGhostSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPlacementManager _placementManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private string _placementMode = typeof(AlignRCDConstruction).Name;
    private Direction _placementDirection = default;

    private const string RpdPlacementMode = nameof(AlignRPDAtmosPipeLayers); // Starlight RPD
    private const string PlacementMode = nameof(AlignRCDConstruction);

    // Starlight Start: RPD
    private bool _useMirrorPrototype = false;
    public event EventHandler? FlipConstructionPrototype;

    public override void Initialize()
    {
        base.Initialize();

        // Bind flip key
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.EditorFlipObject,
                new PointerInputCmdHandler(HandleFlip, outsidePrediction: true))
            .Register<RCDConstructionGhostSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<RCDConstructionGhostSystem>();
        base.Shutdown();
    }

    private bool HandleFlip(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (args.State == BoundKeyState.Down)
        {
            if (!_placementManager.IsActive || _placementManager.Eraser)
                return false;

            var placerEntity = _placementManager.CurrentPermission?.MobUid;

            if (!TryComp<RCDComponent>(placerEntity, out var rcd) ||
                string.IsNullOrEmpty(rcd.CachedPrototype.MirrorPrototype))
                return false;

            _useMirrorPrototype = !rcd.UseMirrorPrototype;

            RaiseNetworkEvent(new RCDConstructionGhostFlipEvent(GetNetEntity(placerEntity.Value), _useMirrorPrototype));
        }

        return true;
    }
    // Starlight End

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Get current placer data
        var placerEntity = _placementManager.CurrentPermission?.MobUid;
        var placerProto = _placementManager.CurrentPermission?.EntityType;
        var placerIsRCD = HasComp<RCDComponent>(placerEntity);

        // Exit if erasing or the current placer is not an RCD (build mode is active)
        if (_placementManager.Eraser || (placerEntity != null && !placerIsRCD))
            return;

        // Determine if player is carrying an RCD in their active hand
        var player = _playerManager.LocalSession?.AttachedEntity;

        if (!TryComp<HandsComponent>(player, out var hands))
            return;

        var heldEntity = hands.ActiveHand?.HeldEntity;

        if (!TryComp<RCDComponent>(heldEntity, out var rcd))
        {
            // If the player was holding an RCD, but is no longer, cancel placement
            if (placerIsRCD)
                _placementManager.Clear();

            return;
        }

        // Starlight edit Start: RPD - use the mirrored prototype if the flip state is toggled on
        // var prototype = _protoManager.Index(rcd.ProtoId);

        // Determine if mirrored
        var cachedProto = rcd.CachedPrototype;
        var wantMirror = _useMirrorPrototype && !string.IsNullOrEmpty(cachedProto.MirrorPrototype);
        var prototype = wantMirror ? cachedProto.MirrorPrototype : cachedProto.Prototype;

        bool isLayered = rcd.IsRPD
                         && _protoManager.TryIndex<RCDPrototype>(cachedProto.ID, out var rcdProto)
                         && rcdProto.HasLayers;

        var desiredMode = isLayered ? RpdPlacementMode : PlacementMode;
        // Starlight edit End: RPD - use the mirrored prototype if the flip state is toggled on

        // Update the direction the RCD prototype based on the placer direction
        if (_placementDirection != _placementManager.Direction)
        {
            _placementDirection = _placementManager.Direction;
            RaiseNetworkEvent(new RCDConstructionGhostRotationEvent(GetNetEntity(heldEntity.Value), _placementDirection));
        }

        // If the placer has not changed, exit
        // Starlight edit Start
        if (heldEntity == placerEntity &&
            prototype == placerProto &&
            _placementManager.CurrentPermission?.PlacementOption == desiredMode)
            // Starlight edit End
            return;

        // Starlight End
        var newObjInfo = new PlacementInformation
        {
            MobUid = heldEntity.Value,
            PlacementOption = desiredMode, // Starlight Edit: PlacementMode -> desiredMode
            EntityType = prototype, // Starlight Edit: prototype.Prototype -> prototype
            Range = (int) Math.Ceiling(SharedInteractionSystem.InteractionRange),
            IsTile = (cachedProto.Mode == RcdMode.ConstructTile), // Starlight Edit: prototype.Mode -> cachedProto.Mode
            UseEditorContext = false,
        };

        _placementManager.Clear();
        _placementManager.BeginPlacing(newObjInfo);
    }
}
