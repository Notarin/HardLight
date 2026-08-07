using System.Linq;
using Castle.Components.DictionaryAdapter.Xml;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._HL.RoundPersistence.SaveBans;
using Content.Server.Storage.EntitySystems;
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
        var sys = Server.System<StorageSystem>();
        var compFact = Server.ResolveDependency<IComponentFactory>();

        await SpawnTarget(BluespaceStashProtoId);
        var entId = ToServer(Target.Value);
        var storage = SEntMan.GetComponent<StorageComponent>(entId);

        // Remove the Use delay
        await Server.WaitPost(() => SEntMan.RemoveComponent<UseDelayComponent>(STarget!.Value));
        await RunTicks(5);

        Assert.That(storage.Container.Count, Is.Zero, "Spawned Bluespace Stash wasn't empty!");
        Assert.That(SEntMan.HasComponent<HLPersistOnShipSaveComponent>(entId), "Bluespace Stash doesn't have Save Component???");

        // Test Banned Entities
        Assert.Multiple(async () =>
        {
            foreach (SaveBanStore.SaveBan ban in SaveBanStore.Bans.Where(x => x.BannedFlag is SaveBanStore.SaveBanFlag.SaveBanFlagByEntity))
            {
                var banFlag = (SaveBanStore.SaveBanFlag.SaveBanFlagByEntity)ban.BannedFlag;
                var protoId = banFlag.Prototype;

                //Ignore non-items for this test.
                var isItem = true;
                await Server.WaitPost(() =>
                {
                    var protos = ProtoMan.EnumeratePrototypes<EntityPrototype>().Where(p => p.ID == protoId && p.HasComponent<ItemComponent>());
                    if (protos.Count() < 1)
                    {
                        isItem = false;
                        return;
                    }
                    var proto = protos.First();
                    // Some items don't start life as one, so ignore em
                    if (proto.TryGetComponent<PhysicsComponent>(out var phys, compFact) && phys.BodyType == Robust.Shared.Physics.BodyType.Static)
                    {
                        isItem = false;
                        return;
                    }
                    if (proto.TryGetComponent<TransformComponent>(out var trans, compFact) && trans.Anchored)
                    {
                        isItem = false;
                        return;
                    }
                });
                if (!isItem)
                {
                    continue;
                }

                await InteractUsing(protoId);
                Assert.That(storage.StoredItems.Count, Is.Zero, $"Bluespace stash got banned item added {protoId} to it!");
                storage.StoredItems.Clear();
                await DeleteHeldEntity();
            }
        });

        // Test Banned Components by adding them to an item and testing, it doesn't actually matter if it's not supposed to be on an item lmao
        Assert.Multiple(async () =>
        {
            foreach (SaveBanStore.SaveBan ban in SaveBanStore.Bans.Where(x => x.BannedFlag is SaveBanStore.SaveBanFlag.SaveBanFlagByComponent))
            {
                var banFlag = (SaveBanStore.SaveBanFlag.SaveBanFlagByComponent)ban.BannedFlag;
                var compName = banFlag.Name;
                var testItem = await PlaceInHands(ComponentTestItem);
                var testItemS = ToServer(testItem);

                var comp = Factory.GetComponent(Factory.AllRegisteredTypes.First(c => Factory.GetComponentName(c) == compName));
                await Server.WaitPost(() =>
                {
                    SEntMan.AddComponent(testItemS, comp);
                });

                Assert.That(SEntMan.HasComponent(testItemS, comp.GetComponentType()), $"Test Item could not take component {compName}");
                await Interact();

                Assert.That(storage.StoredItems.Count, Is.Zero, $"Bluespace stash got banned item with component {testItem} added to it!");
                storage.StoredItems.Clear();
                await DeleteHeldEntity();
            }
        });
    }
}
