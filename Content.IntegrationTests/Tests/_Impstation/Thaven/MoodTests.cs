// SPDX-FileCopyrightText: 2025 ATDoop <bug@bug.bug>
// SPDX-FileCopyrightText: 2025 corresp0nd <46357632+corresp0nd@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Content.Server._Impstation.Thaven;
using Content.Shared.Dataset;
using Content.Shared._Impstation.Thaven;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Impstation.Thaven;

[TestFixture, TestOf(typeof(ThavenMoodPrototype))]
public sealed class ThavenMoodTests
{
    private static readonly ProtoId<DatasetPrototype> ThreeValueSetId = "ThreeValueSet";
    private static readonly ProtoId<ThavenMoodPrototype> DuplicateTestId = "DuplicateTest";
    private static readonly ProtoId<ThavenMoodPrototype> DuplicateOverlapTestId = "DuplicateOverlapTest";

    [TestPrototypes]
    const string PROTOTYPES = @"
- type: dataset
  id: ThreeValueSet
  values:
    - One
    - Two
    - Three
- type: thavenMood
  id: DuplicateTest
  moodName: DuplicateTest
  moodDesc: DuplicateTest
  allowDuplicateMoodVars: false
  moodVars:
    a: ThreeValueSet
    b: ThreeValueSet
    c: ThreeValueSet
- type: thavenMood
  id: DuplicateOverlapTest
  moodName: DuplicateOverlapTest
  moodDesc: DuplicateOverlapTest
  allowDuplicateMoodVars: false
  moodVars:
    a: ThreeValueSet
    b: ThreeValueSet
    c: ThreeValueSet
    d: ThreeValueSet
    e: ThreeValueSet
";

    [Test]
    [Repeat(10)]
    public async Task TestDuplicatePrevention()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entMan = server.ResolveDependency<IEntityManager>();
        var thavenSystem = entMan.System<ThavenMoodsSystem>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var dataset = protoMan.Index<DatasetPrototype>(ThreeValueSetId);
            var moodProto = protoMan.Index<ThavenMoodPrototype>(DuplicateTestId);

            var datasetSet = dataset.Values.ToHashSet();
            var mood = thavenSystem.RollMood(moodProto);
            var moodVarSet = mood.MoodVars.Values.ToHashSet();

            Assert.That(moodVarSet, Is.EquivalentTo(datasetSet));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    [Repeat(10)]
    public async Task TestDuplicateOverlap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var thavenSystem = entMan.System<ThavenMoodsSystem>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var dataset = protoMan.Index<DatasetPrototype>(ThreeValueSetId);
            var moodProto = protoMan.Index<ThavenMoodPrototype>(DuplicateOverlapTestId);

            var datasetSet = dataset.Values.ToHashSet();
            var mood = thavenSystem.RollMood(moodProto);
            var moodVarSet = mood.MoodVars.Values.ToHashSet();

            Assert.That(moodVarSet, Is.EquivalentTo(datasetSet));
        });

        await pair.CleanReturnAsync();
    }
}
