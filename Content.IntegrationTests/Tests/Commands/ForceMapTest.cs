using Content.Server.Maps;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.IntegrationTests.Tests.Commands;

[TestFixture]
public sealed class ForceMapTest
{
    private const string DefaultMapName = "Empty";
    private const string BadMapName = "asdf_asd-fa__sdfAsd_f"; // Hopefully no one ever names a map this...
    private const string TestMapEligibleName = "ForceMapTestEligible";
    private const string TestMapIneligibleName = "ForceMapTestIneligible";

    [TestPrototypes]
    private static readonly string TestMaps = @$"
- type: gameMap
  id: {TestMapIneligibleName}
  mapName: {TestMapIneligibleName}
  mapPath: /Maps/Test/empty.yml
  minPlayers: 20
  maxPlayers: 80
  stations:
    Empty:
      stationProto: StandardNanotrasenStation
      components:
        - type: StationNameSetup
          mapNameTemplate: ""Empty""

- type: gameMap
  id: {TestMapEligibleName}
  mapName: {TestMapEligibleName}
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations:
    Empty:
      stationProto: StandardNanotrasenStation
      components:
        - type: StationNameSetup
          mapNameTemplate: ""Empty""
";

    // HL: Swapped GetSelectedMap for GetCVar, as SelectedMap isn't always the map we're on, due to round persistence
    [Test]
    public async Task TestForceMapCommand()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.EntMan;
        var configManager = server.ResolveDependency<IConfigurationManager>();
        var consoleHost = server.ResolveDependency<IConsoleHost>();
        var gameMapMan = server.ResolveDependency<IGameMapManager>();

        await server.WaitAssertion(() =>
        {
            // Make sure we're set to the default map
            Assert.That(configManager.GetCVar(CCVars.GameMap), Is.EqualTo(DefaultMapName), // HL
                $"Test didn't start on expected map ({DefaultMapName})!");

            // Try changing to a map that doesn't exist
            consoleHost.ExecuteCommand($"forcemap {BadMapName}");
            Assert.That(configManager.GetCVar(CCVars.GameMap), Is.EqualTo(DefaultMapName), // HL
                $"Forcemap succeeded with a map that does not exist ({BadMapName})!");

            // Try changing to a valid map
            consoleHost.ExecuteCommand($"forcemap {TestMapEligibleName}");
            Assert.That(configManager.GetCVar(CCVars.GameMap), Is.EqualTo(TestMapEligibleName), // HL
                $"Forcemap failed with a valid map ({TestMapEligibleName})");

            // Try changing to a map that exists but is ineligible
            consoleHost.ExecuteCommand($"forcemap {TestMapIneligibleName}");
            Assert.That(configManager.GetCVar(CCVars.GameMap), Is.EqualTo(TestMapIneligibleName), // HL
                $"Forcemap failed with valid but ineligible map ({TestMapIneligibleName})!");

            // Try clearing the force-selected map
            consoleHost.ExecuteCommand("forcemap \"\"");
            Assert.That(configManager.GetCVar(CCVars.GameMap), Is.EqualTo(""), /// HL
                $"Running 'forcemap \"\"' did not clear the forced map!");

        });

        // Cleanup
        configManager.SetCVar(CCVars.GameMap, DefaultMapName);

        await pair.CleanReturnAsync();
    }

}
