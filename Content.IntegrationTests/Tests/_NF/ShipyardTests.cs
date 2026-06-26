using System.Runtime.CompilerServices;
using System.Text.Json;
using Content.Server._NF.Shipyard.Systems;
using Content.Server.Cargo.Systems;
using Content.Shared._NF.CCVar;
using Content.Shared._NF.Shipyard.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._NF;

// HL: I hate this SO much, but I'd rather use the same method we actually use to calculate price rather than re-implement it.
// Using reflection to access a private method in the ShipyardSystem so we can get the ship price
public static class ShipyardSystemAccessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AppraiseGridForShipyardSale")]
    public static extern double CallAppraiseGridForShipyardSale(ShipyardSystem target, EntityUid ent);
}

[TestFixture]
public sealed class ShipyardTest
{
    [Test]
    public async Task CheckAllShuttleGrids()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapLoader = entManager.System<MapLoaderSystem>();
        var map = entManager.System<MapSystem>();

        await server.WaitPost(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var vessel in protoManager.EnumeratePrototypes<VesselPrototype>())
                {
                    if (vessel.ShuttlePath.Filename == "empty.yml") // HL: Ignore any vessels using the test map
                        continue;
                    map.CreateMap(out var mapId);

                    bool mapLoaded = false;
                    Entity<MapGridComponent>? shuttle = null;
                    try
                    {
                        mapLoaded = mapLoader.TryLoadGrid(mapId, vessel.ShuttlePath, out shuttle);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Failed to load shuttle {vessel} ({vessel.ShuttlePath}): TryLoadGrid threw exception {ex}");
                        map.DeleteMap(mapId);
                        continue;
                    }

                    Assert.That(mapLoaded, Is.True, $"Failed to load shuttle {vessel} ({vessel.ShuttlePath}): TryLoadGrid returned false.");
                    Assert.That(shuttle.HasValue, Is.True);
                    Assert.That(entManager.HasComponent<MapGridComponent>(shuttle.Value), Is.True);

                    try
                    {
                        map.DeleteMap(mapId);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Failed to delete map for {vessel} ({vessel.ShuttlePath}): {ex}");
                    }
                }
            });
        });
        await server.WaitRunTicks(1);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NoShipyardShipArbitrage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapLoader = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<MapLoaderSystem>();
        var map = entManager.System<MapSystem>();
        var shipyard = entManager.System<ShipyardSystem>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var pricing = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<PricingSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var vessel in protoManager.EnumeratePrototypes<VesselPrototype>())
                {
                    if (vessel.ShuttlePath.Filename == "empty.yml") // HL: Ignore any vessels using the test map
                        continue;

                    map.CreateMap(out var mapId);
                    double appraisePrice = 0;

                    bool mapLoaded = false;
                    Entity<MapGridComponent>? shuttle = null;
                    try
                    {
                        mapLoaded = mapLoader.TryLoadGrid(mapId, vessel.ShuttlePath, out shuttle);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Failed to load shuttle {vessel} ({vessel.ShuttlePath}): TryLoadGrid threw exception {ex}");
                        map.DeleteMap(mapId);
                        continue;
                    }
                    Assert.That(mapLoaded, Is.True, $"Failed to load shuttle {vessel} ({vessel.ShuttlePath}): TryLoadGrid returned false.");
                    Assert.That(entManager.HasComponent<MapGridComponent>(shuttle.Value), Is.True);

                    // Grid failed to load, continue to the next map.
                    if (!mapLoaded)
                        continue;

                    pricing.AppraiseGrid(shuttle.Value, null, (uid, price) =>
                    {
                        appraisePrice += price;
                    });
                    var salePrice = ShipyardSystemAccessors.CallAppraiseGridForShipyardSale(shipyard, shuttle.Value); // HL: using reflection so we can use the same method the game uses to calculate price
                    salePrice *= cfg.GetCVar(NFCCVars.ShipyardSellRate);
                    var idealMinPrice = appraisePrice * vessel.MinPriceMarkup;
                    var roundedMinPrice = Math.Ceiling(idealMinPrice / 500.0) * 500; // HL: Round up to the nearest 500

                    Assert.That(vessel.Price, Is.AtLeast(salePrice),
                        $"Arbitrage possible on {vessel.ID}. Sale price is be {Math.Round(salePrice)}, but buy price is {vessel.Price}. Purchase Price Should be at least: {roundedMinPrice}");

                    map.DeleteMap(mapId);
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    /* HL:
    // Using this to generate a list of reccomended ship prices, currently being cross-referenced manually.
    // Ideally this would actually generate an output file that we can use to generate a patch file, but I haven't gotten that far yet.
    */
    [Test]
    [Explicit("This is just for generating the prices to feed into the ship price updater")]
    public async Task CalculateShipPrices()
    {
        Func<double, double, double> calcSalePrice = delegate (double salePrice, double markup)
        {
            var markupPrice = salePrice * markup;
            var roundedPrice = Math.Ceiling(markupPrice / 500.0) * 500; // Round up to the nearest 500
            return roundedPrice;
        };

        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapLoader = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<MapLoaderSystem>();
        var map = entManager.System<MapSystem>();
        var shipyard = entManager.System<ShipyardSystem>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var pricing = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<PricingSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var vessel in protoManager.EnumeratePrototypes<VesselPrototype>())
                {
                    if (vessel.ShuttlePath.Filename == "empty.yml")
                        continue;

                    map.CreateMap(out var mapId);
                    double appraisePrice = 0;

                    bool mapLoaded = false;
                    Entity<MapGridComponent>? shuttle = null;
                    try
                    {
                        mapLoaded = mapLoader.TryLoadGrid(mapId, vessel.ShuttlePath, out shuttle);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Failed to load shuttle {vessel} ({vessel.ShuttlePath}): TryLoadGrid threw exception {ex}");
                        map.DeleteMap(mapId);
                        continue;
                    }

                    // Grid failed to load, continue to the next map.
                    if (!mapLoaded)
                        continue;
                    if (!entManager.HasComponent<MapGridComponent>(shuttle.Value))
                        continue;

                    pricing.AppraiseGrid(shuttle.Value, null, (uid, price) =>
                    {
                        appraisePrice += price;
                    });
                    var salePrice = ShipyardSystemAccessors.CallAppraiseGridForShipyardSale(shipyard, shuttle.Value); // HL: Use reflection to get the ship price from the system, it's a private function for reasons
                    salePrice *= cfg.GetCVar(NFCCVars.ShipyardSellRate);
                    var recSalePrice = calcSalePrice(salePrice, vessel.MinPriceMarkup);
                    Console.WriteLine($"ShipSale,{vessel.ID},{vessel.Price},{salePrice},{recSalePrice}");

                    map.DeleteMap(mapId);
                }
            });
        });
        await pair.CleanReturnAsync();
    }
}
