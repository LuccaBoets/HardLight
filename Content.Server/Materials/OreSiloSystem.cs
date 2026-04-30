using System.Linq;
using Content.Server.Pinpointer;
using Content.Shared.IdentityManagement;
using Content.Shared.Materials.OreSilo;
using Robust.Server.GameStates;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Materials;

/// <inheritdoc/>
public sealed class OreSiloSystem : SharedOreSiloSystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float OreSiloPreloadRangeSquared = 225f; // ~1 screen
    private const float ValidationInterval = 30f; // Validate connections every 30 seconds
    private const float PvsPreloadInterval = 2f; // Re-check PVS preloads every 2s, not every tick

    private readonly HashSet<Entity<OreSiloClientComponent>> _clientLookup = new();
    private readonly HashSet<(NetEntity, string, string)> _clientInformation = new();
    private readonly HashSet<EntityUid> _silosToAdd = new();
    private readonly HashSet<EntityUid> _silosToRemove = new();

    // Tracks which silos each session had in range on the previous preload scan.
    // We only call Add/Remove when the set actually changes, avoiding per-tick PVS churn.
    private readonly Dictionary<ICommonSession, HashSet<EntityUid>> _sessionSilosInRange = new();

    private TimeSpan _nextValidation = TimeSpan.Zero;
    private TimeSpan _nextPvsPreload = TimeSpan.Zero;

    protected override void UpdateOreSiloUi(Entity<OreSiloComponent> ent)
    {
        if (!_userInterface.IsUiOpen(ent.Owner, OreSiloUiKey.Key))
            return;
        _clientLookup.Clear();
        _clientInformation.Clear();

        var xform = Transform(ent);

        // Sneakily uses override with TComponent parameter
        _entityLookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.Range, _clientLookup);

        foreach (var client in _clientLookup)
        {
            // don't show already-linked clients.
            if (client.Comp.Silo is not null)
                continue;

            // Don't show clients on the screen if we can't link them.
            if (!CanTransmitMaterials((ent, ent, xform), client))
                continue;

            var netEnt = GetNetEntity(client);
            var name = Identity.Name(client, EntityManager);
            var beacon = _navMap.GetNearestBeaconString(client.Owner, onlyName: true);

            var txt = Loc.GetString("ore-silo-ui-nf-itemlist-entry", // Frontier: use NF key
                ("name", name),
                // ("beacon", beacon), // Frontier
                ("linked", ent.Comp.Clients.Contains(client)),
                ("inRange", true));

            _clientInformation.Add((netEnt, txt, beacon));
        }

        // Get all clients of this silo, including those out of range.
        foreach (var client in ent.Comp.Clients)
        {
            var netEnt = GetNetEntity(client);
            var name = Identity.Name(client, EntityManager);
            var beacon = _navMap.GetNearestBeaconString(client, onlyName: true);
            var inRange = CanTransmitMaterials((ent, ent, xform), client);

            var txt = Loc.GetString("ore-silo-ui-nf-itemlist-entry", // Frontier: use NF key
                ("name", name),
                // ("beacon", beacon), // Frontier
                ("linked", ent.Comp.Clients.Contains(client)),
                ("inRange", inRange));

            _clientInformation.Add((netEnt, txt, beacon));
        }

        _userInterface.SetUiState(ent.Owner, OreSiloUiKey.Key, new OreSiloBuiState(_clientInformation));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        // Periodically validate all silo-client connections
        if (curTime >= _nextValidation)
        {
            _nextValidation = curTime + TimeSpan.FromSeconds(ValidationInterval);
            ValidateAllConnections();
        }

        // Rate-limit the PVS preload scan. This is an O(players × silo_clients) loop and does not
        // need to run every tick — 2-second granularity is fine for ore silo visibility.
        // Previously this ran 30×/sec and was a top-3 server CPU cost on populated maps.
        if (curTime < _nextPvsPreload)
            return;

        _nextPvsPreload = curTime + TimeSpan.FromSeconds(PvsPreloadInterval);

        // Solving an annoying problem: we need to send the silo to people who are near the silo so that
        // Things don't start wildly mispredicting. We do this as cheaply as possible via grid-based local-pos checks.
        // Sloth okay-ed this in the interim until a better solution comes around.

        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out _, out var actorComp, out var actorXform))
        {
            var session = actorComp.PlayerSession;
            if (session == null)
                continue;

            _silosToAdd.Clear();
            _silosToRemove.Clear();

            // Build the set of silos currently in range for this session.
            var nowInRange = new HashSet<EntityUid>();
            var clientQuery = EntityQueryEnumerator<OreSiloClientComponent, TransformComponent>();
            while (clientQuery.MoveNext(out _, out var clientComp, out var clientXform))
            {
                if (clientComp.Silo == null)
                    continue;

                // We limit it to same-grid checks only for peak perf
                if (actorXform.GridUid != clientXform.GridUid)
                    continue;

                if ((actorXform.LocalPosition - clientXform.LocalPosition).LengthSquared() <= OreSiloPreloadRangeSquared)
                    nowInRange.Add(clientComp.Silo.Value);
            }

            // Only call Add/Remove for silos whose in-range status actually changed since the last scan.
            if (!_sessionSilosInRange.TryGetValue(session, out var prevInRange))
                prevInRange = new HashSet<EntityUid>();

            foreach (var silo in nowInRange)
            {
                if (!prevInRange.Contains(silo))
                    _silosToAdd.Add(silo);
            }

            foreach (var silo in prevInRange)
            {
                if (!nowInRange.Contains(silo))
                    _silosToRemove.Add(silo);
            }

            foreach (var toRemove in _silosToRemove)
                _pvsOverride.RemoveSessionOverride(toRemove, session);

            foreach (var toAdd in _silosToAdd)
                _pvsOverride.AddSessionOverride(toAdd, session);

            _sessionSilosInRange[session] = nowInRange;
        }

        // Prune sessions that have disconnected.
        foreach (var session in _sessionSilosInRange.Keys.ToList())
        {
            if (session.Status == SessionStatus.Disconnected)
                _sessionSilosInRange.Remove(session);
        }
    }

    /// <summary>
    /// Validates all silo-client connections and removes invalid ones.
    /// This helps recover from situations where grids are respawned or entity references become stale.
    /// </summary>
    private void ValidateAllConnections()
    {
        var clientQuery = EntityQueryEnumerator<OreSiloClientComponent>();
        while (clientQuery.MoveNext(out var clientUid, out var clientComp))
        {
            if (clientComp.Silo is not { } silo)
                continue;

            // Check if silo still exists
            if (!Exists(silo) || !TryComp<OreSiloComponent>(silo, out var siloComp))
            {
                clientComp.Silo = null;
                Dirty(clientUid, clientComp);
                continue;
            }

            // Ensure bidirectional link
            if (!siloComp.Clients.Contains(clientUid))
            {
                siloComp.Clients.Add(clientUid);
                Dirty(silo, siloComp);
            }

            // Validate connection criteria
            if (!CanTransmitMaterials((silo, siloComp, null), clientUid))
            {
                clientComp.Silo = null;
                siloComp.Clients.Remove(clientUid);
                Dirty(clientUid, clientComp);
                Dirty(silo, siloComp);
            }
        }

        // Also clean up orphaned references in silos
        var siloQuery = EntityQueryEnumerator<OreSiloComponent>();
        while (siloQuery.MoveNext(out var siloUid, out var siloComp))
        {
            var toRemove = new List<EntityUid>();
            foreach (var client in siloComp.Clients)
            {
                if (!Exists(client) || !TryComp<OreSiloClientComponent>(client, out var clientComp))
                {
                    toRemove.Add(client);
                    continue;
                }

                // Ensure client points back to this silo
                if (clientComp.Silo != siloUid)
                {
                    toRemove.Add(client);
                }
            }

            if (toRemove.Count > 0)
            {
                foreach (var client in toRemove)
                {
                    siloComp.Clients.Remove(client);
                }
                Dirty(siloUid, siloComp);
            }
        }
    }
}
