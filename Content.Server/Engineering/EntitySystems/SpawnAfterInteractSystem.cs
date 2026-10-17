using Content.Server.Engineering.Components;
using Content.Server.Stack;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Engineering.EntitySystems
{
    [UsedImplicitly]
    public sealed class SpawnAfterInteractSystem : EntitySystem
    {
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly StackSystem _stackSystem = default!;
        [Dependency] private readonly TurfSystem _turfSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SharedMapSystem _maps = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SpawnAfterInteractComponent, AfterInteractEvent>(HandleAfterInteract);
            SubscribeLocalEvent<SpawnAfterInteractComponent, SpawnAfterInteractDoAfterEvent>(OnDoAfter);
        }

        private void HandleAfterInteract(EntityUid uid, SpawnAfterInteractComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach && !component.IgnoreDistance)
                return;
            if (string.IsNullOrEmpty(component.Prototype))
                return;

            var gridUid = _transform.GetGrid(args.ClickLocation);
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                return;
            if (!_maps.TryGetTileRef(gridUid.Value, grid, args.ClickLocation, out var tileRef))
                return;

            bool IsTileClear()
            {
                return tileRef.Tile.IsEmpty == false && !_turfSystem.IsTileBlocked(tileRef, CollisionGroup.MobMask);
            }

            if (!IsTileClear())
                return;

            if (component.DoAfterTime > 0)
            {
                var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.DoAfterTime, new SpawnAfterInteractDoAfterEvent(GetNetCoordinates(args.ClickLocation)), uid, target: args.Target, used: uid)
                {
                    BreakOnMove = true,
                };
                _doAfterSystem.TryStartDoAfter(doAfterArgs);
                return;
            }

            TrySpawn(uid, component, args.ClickLocation, grid);
        }

        private void OnDoAfter(EntityUid uid, SpawnAfterInteractComponent component, SpawnAfterInteractDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled || component.Deleted)
                return;

            var clickLocation = GetCoordinates(args.ClickLocation);
            var gridUid = _transform.GetGrid(clickLocation);
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                return;
            if (!_maps.TryGetTileRef(gridUid.Value, grid, clickLocation, out var tileRef))
                return;

            if (tileRef.Tile.IsEmpty || _turfSystem.IsTileBlocked(tileRef, CollisionGroup.MobMask))
                return;

            TrySpawn(uid, component, clickLocation, grid);
            args.Handled = true;
        }

        private void TrySpawn(EntityUid uid, SpawnAfterInteractComponent component, EntityCoordinates clickLocation, MapGridComponent grid)
        {
            if (component.Deleted)
                return;

            if (TryComp(uid, out StackComponent? stackComp)
                && component.RemoveOnInteract && !_stackSystem.Use(uid, 1, stackComp))
            {
                return;
            }

            Spawn(component.Prototype, clickLocation.SnapToGrid(grid));

            if (component.RemoveOnInteract && stackComp == null)
                QueueDel(uid); // Frontier: TryQueueDel<QueueDel
        }
    }
}
