using System.Reflection;
using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Shared.Random;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.IntegrationTests.Tests._HL;

[TestFixture]
[TestOf(typeof(MutationSystem))]
public sealed class MutationSystemTests
{
    private const string PlantHolderId = "MutationPlantHolderDummy";
    private const string RandomMutationListId = "MutationChainRandomPlantMutations";
    private const string SeedAId = "MutationSeedA";
    private const string SeedBId = "MutationSeedB";
    private const string SeedCId = "MutationSeedC";
    private const string SeedAName = "Mutation Seed A";
    private const string SeedCName = "Mutation Seed C";

    private static readonly FieldInfo RandomMutationsField =
        typeof(MutationSystem).GetField("_randomMutations", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: MutationPlantHolderDummy
  name: MutationPlantHolderDummy
  components:
    - type: PlantHolder

- type: RandomPlantMutationList
  id: MutationChainRandomPlantMutations
  mutations:
    - name: ChangeSpecies
      baseOdds: 1.0
      appliesToProduce: false
      persists: false
      effect: !type:PlantSpeciesChange

- type: seed
  id: MutationSeedA
  name: Mutation Seed A
  displayName: Mutation Seed A
  plantRsi: Objects/Specific/Hydroponics/wheat.rsi
  mutationPrototypes:
    - MutationSeedB

- type: seed
  id: MutationSeedB
  name: Mutation Seed B
  displayName: Mutation Seed B
  plantRsi: Objects/Specific/Hydroponics/wheat.rsi
  mutationPrototypes:
    - MutationSeedC

- type: seed
  id: MutationSeedC
  name: Mutation Seed C
  displayName: Mutation Seed C
  plantRsi: Objects/Specific/Hydroponics/wheat.rsi
";

    [Test]
    public async Task CheckRandomMutationsRestartsSubtypePass()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var serializationManager = server.ResolveDependency<ISerializationManager>();

        EntityUid plant = default;

        await server.WaitPost(() =>
        {
            var mutationSystem = server.System<MutationSystem>();
            RandomMutationsField.SetValue(mutationSystem,
                prototypeManager.Index<RandomPlantMutationListPrototype>(RandomMutationListId));

            plant = entityManager.SpawnEntity(PlantHolderId, MapCoordinates.Nullspace);

            var plantHolder = entityManager.GetComponent<PlantHolderComponent>(plant);
            SeedData seed = serializationManager.CreateCopy(
              prototypeManager.Index<SeedPrototype>(SeedAId),
              notNullableOverride: true);
            plantHolder.Seed = seed;

            Assert.That(seed.Name, Is.EqualTo(SeedAName));

            mutationSystem.CheckRandomMutations(plant, ref seed, 1f);
        });

        await server.WaitAssertion(() =>
        {
            var plantHolder = entityManager.GetComponent<PlantHolderComponent>(plant);
            Assert.That(plantHolder.Seed, Is.Not.Null);
            Assert.That(plantHolder.Seed!.Name, Is.EqualTo(SeedCName));
        });

        await pair.CleanReturnAsync();
    }
}