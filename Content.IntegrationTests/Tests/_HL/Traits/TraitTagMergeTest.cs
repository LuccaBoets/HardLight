using System.Linq;
using Content.Server.Traits;
using Content.Shared.Tag;
using Content.Shared.Traits;
using Content.Shared.Whitelist;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Traits;

[TestFixture]
[TestOf(typeof(TraitSystem))]
public sealed class TraitTagMergeTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: Tag
  id: TestCanPilot

- type: Tag
  id: TestSpiderCraft

- type: entity
  id: TraitTagMergeDummy
  components:
  - type: Tag
    tags:
    - TestCanPilot

- type: trait
  id: TestBionicSpinarette
  name: trait-test-bionic-spinarette-name
  description: trait-test-bionic-spinarette-desc
  cost: 0
  components:
  - type: Tag
    tags:
    - TestSpiderCraft
";

    [Test]
    public async Task TraitAddsTagWithoutOverwritingExistingTags()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var testMap = await pair.CreateTestMap();
        var mapCoordinates = testMap.MapCoords;

        await server.WaitAssertion(() =>
        {
            var entity = server.EntMan.SpawnEntity("TraitTagMergeDummy", mapCoordinates);
            var traitSystem = server.System<TraitSystem>();
            var tagSystem = server.System<TagSystem>();
            var whitelistSystem = server.System<EntityWhitelistSystem>();
            var prototypeManager = server.Resolve<IPrototypeManager>();

            var proto = prototypeManager.Index<TraitPrototype>("TestBionicSpinarette");
            Assert.That(tagSystem.HasTag(entity, "TestCanPilot"), Is.True);
            Assert.That(tagSystem.HasTag(entity, "TestSpiderCraft"), Is.False);

            traitSystem.AddTrait(entity, proto);

            Assert.Multiple(() =>
            {
                Assert.That(tagSystem.HasTag(entity, "TestCanPilot"), Is.True);
                Assert.That(tagSystem.HasTag(entity, "TestSpiderCraft"), Is.True);
                Assert.That(whitelistSystem.IsValid(new EntityWhitelist { Tags = new() { "TestSpiderCraft" } }, entity), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }
}
