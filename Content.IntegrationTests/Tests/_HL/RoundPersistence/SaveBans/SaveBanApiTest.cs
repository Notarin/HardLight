using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._HL.RoundPersistence.SaveBans;
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
}
