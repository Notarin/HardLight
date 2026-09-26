using Content.IntegrationTests.Tests.Interaction;
using Content.Server._HL.RoundPersistence.SaveBans;
using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Shared._HL.RoundPersistence.SaveBans;
using Content.Shared.Atmos;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using static Content.Server._HL.RoundPersistence.SaveBans.SaveBanApi.SaveBanResult;

namespace Content.IntegrationTests.Tests.RoundPersistence.SaveBans;

[TestFixture]
[TestOf(typeof(SaveBanApi))]
public sealed class SaveBanApiTest : InteractionTest
{

    [Test]
    public async Task EverySaveBanComponentNameIsRegistered()
    {
        var api = Server.System<SaveBanApi>();

        foreach (var ban in api.Bans.Where(x => x.BannedFlag is SaveBanStore.SaveBanFlag.SaveBanFlagByComponent))
        {
            var component = ((SaveBanStore.SaveBanFlag.SaveBanFlagByComponent)ban.BannedFlag).Name;

            Assert.That(Factory.AllRegisteredTypes.Any(t => Factory.GetComponentName(t) == component),
                $"Component '{component}' is not registered!");
        }
    }

    [Test]
    public async Task ChinaLake_IsSaveBanned()
    {
        var api = Server.System<SaveBanApi>();

        EntityUid uid = default;
        await Server.WaitPost(Post);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.CheckForRestrictions(uid), Is.TypeOf<IsSaveRestricted>());
            Assert.That(((IsSaveRestricted)api.CheckForRestrictions(uid)!).Ban.BannedFlag, Is.TypeOf<SaveBanStore.SaveBanFlag.SaveBanFlagByEntity>());
        }
        return;

        void Post() => uid = SEntMan.SpawnEntity("WeaponLauncherChinaLake", MapCoordinates.Nullspace);
    }

    [Test]
    public async Task SeedWithBannedGas_IsSaveBanned()
    {
        SaveBanApi api = Server.System<SaveBanApi>();

        EntityUid uid = default;
        await Server.WaitPost(Post);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.CheckForRestrictions(uid), Is.TypeOf<IsSaveRestricted>());
            Assert.That(((IsSaveRestricted)api.CheckForRestrictions(uid)!).Ban.BannedFlag,
                Is.TypeOf<SaveBanStore.SaveBanFlag.SaveBanSeedFlag.SaveBanSeedFlagByGasProduction>());
        }

        return;

        void Post()
        {
            uid = SEntMan.SpawnEntity("FoodApple", MapCoordinates.Nullspace);
            SeedComponent seedComp = SEntMan.AddComponent<SeedComponent>(uid);

            SeedData seedData = new();
            typeof(SeedData).GetField("ExudeGasses")
                ?.SetValue(seedData, new Dictionary<Gas, float>
                {
                    { Gas.Tritium, 1f }
                });

            typeof(SeedComponent).GetField("Seed")?.SetValue(seedComp, seedData);
        }
    }

    [Test]
    public async Task SeedWithBannedReagent_IsSaveBanned()
    {
        SaveBanApi api = Server.System<SaveBanApi>();

        // Inject a dummy ban so we don't modify the real prototypes for a test.
        api.Bans = api.Bans.Concat(new[]
        {
            new SaveBanStore.SaveBan(
                new SaveBanStore.SaveBanFlag.SaveBanSeedFlag.SaveBanSeedFlagByReagent("Phalanximine"),
                "Test Reason",
                new SaveBanStore.SaveRestrictionStrictness.TotalBan())
        }).ToList();

        EntityUid uid = default;
        await Server.WaitPost(Post);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.CheckForRestrictions(uid), Is.TypeOf<IsSaveRestricted>());
            Assert.That(((IsSaveRestricted)api.CheckForRestrictions(uid)!).Ban.BannedFlag,
                Is.TypeOf<SaveBanStore.SaveBanFlag.SaveBanSeedFlag.SaveBanSeedFlagByReagent>());
        }

        return;

        void Post()
        {
            uid = SEntMan.SpawnEntity("FoodApple", MapCoordinates.Nullspace);
            SeedComponent seedComp = SEntMan.AddComponent<SeedComponent>(uid);

            SeedData seedData = new();
            typeof(SeedData).GetField("Chemicals")
                ?.SetValue(seedData, new Dictionary<string, SeedChemQuantity>
                {
                    { "Phalanximine", new SeedChemQuantity() }
                });

            typeof(SeedComponent).GetField("Seed")?.SetValue(seedComp, seedData);
        }
    }
}
