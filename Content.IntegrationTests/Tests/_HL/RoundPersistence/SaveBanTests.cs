using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._HL.RoundPersistence.SaveBans;
using Content.Shared._HL.Shipyard;
using Content.Shared.Item;
using Content.Shared.Prototypes;
using Content.Shared.Storage;
using Content.Shared.Timing;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RoundPersistence;

[TestFixture]
public sealed class SaveBanTests : InteractionTest
{
    private const string BluespaceStashProtoId = "bluespacestash";
    private const string ComponentTestItem = "DrinkColaCan";

    [Test]
    public async Task TestInsertBannedItemToStash()
    {
        var compFact = Server.ResolveDependency<IComponentFactory>();
        var api = Server.System<SaveBanApi>();

        await SpawnTarget(BluespaceStashProtoId);
        var entId = ToServer(Target.Value);
        var storage = SEntMan.GetComponent<StorageComponent>(entId);

        // Drop UseDelayComponent, causes issues in tests (delay + transactional unit = no good)
        await Server.WaitPost(() => SEntMan.RemoveComponent<UseDelayComponent>(STarget!.Value));

        // State-of-the-art race condition prevention algorithm
        await RunTicks(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(storage.Container.Count, Is.Zero, "Spawned Bluespace Stash wasn't empty!");
            Assert.That(SEntMan.HasComponent<HLPersistOnShipSaveComponent>(entId), "Bluespace Stash doesn't have Save Component???");
        }

        using (Assert.EnterMultipleScope())
        {
            var validItemsToTest = new List<string>();
            await Server.WaitPost(() =>
            {
                validItemsToTest = api.Bans
                    .Select(b => b.BannedFlag)
                    .OfType<SaveBanStore.SaveBanFlag.SaveBanFlagByEntity>()
                    .Select(flag => flag.Prototype)
                    .Select(id => ProtoMan.TryIndex<EntityPrototype>(id, out var proto) ? proto : null)
                    // We must filter for valid test items to prevent failures caused by
                    // attempting to pick up items that are anchored or aren't hold-able
                    .Where(proto => proto != null && IsValidTestItem(proto))
                    .Select(proto => proto!.ID)
                    .ToList();
            });

            foreach (var protoId in validItemsToTest)
            {
                await InteractUsing(protoId);
                Assert.That(storage.StoredItems, Is.Empty, $"Bluespace stash accepted banned item: {protoId}");

                storage.StoredItems.Clear();
                await DeleteHeldEntity();
            }

            var bannedComponentNames = api.Bans
                .Select(b => b.BannedFlag)
                .OfType<SaveBanStore.SaveBanFlag.SaveBanFlagByComponent>()
                .Select(flag => flag.Name)
                .ToList();

            foreach (var compName in bannedComponentNames)
            {
                var testItem = await PlaceInHands(ComponentTestItem);
                var testItemS = ToServer(testItem);
                var isCompAdded = false;

                await Server.WaitPost(() =>
                {
                    var compType = compFact.GetRegistration(compName).Type;
                    var comp = compFact.GetComponent(compType);

                    // Slap the banned component onto a guaranteed valid item (Cola)
                    // Control group item to ensure the component is what matters
                    SEntMan.AddComponent(testItemS, comp);
                    isCompAdded = SEntMan.HasComponent(testItemS, compType);
                });

                Assert.That(isCompAdded, $"Test Item could not take component {compName}");

                await Interact();
                Assert.That(storage.StoredItems, Is.Empty, $"Bluespace stash accepted item with banned component: {compName}");

                storage.StoredItems.Clear();
                await DeleteHeldEntity();
            }
        }
        return;

        // Ensures the item is physically capable of being picked up.
        // Failsafe against anchored entities.
        bool IsValidTestItem(EntityPrototype proto) =>
            proto.HasComponent<ItemComponent>() &&
            !(proto.TryGetComponent<PhysicsComponent>(out var phys, compFact) && phys.BodyType == Robust.Shared.Physics.BodyType.Static) &&
            !(proto.TryGetComponent<TransformComponent>(out var trans, compFact) && trans.Anchored);
    }
}
