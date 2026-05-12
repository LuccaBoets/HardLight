using Content.Server._NF.CryoSleep;
using Content.Server._HL.ColComm; // HardLight
using Content.Server.Afk;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._NF.Roles.Components;
using Content.Shared._NF.Roles.Systems;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network; // HardLight
using Robust.Shared.Prototypes;

namespace Content.Server._NF.Roles.Systems;

/// HardLight start: Rewritten
/// <summary>
/// Handles job slot open/close lifecycle for tracked station jobs.
/// All slot operations are routed through the persistent
/// <see cref="ColcommJobRegistryComponent"/> on the ColComm grid entity,
/// which survives round transitions and avoids stale EntityUid issues.
/// </summary>
// HardLight end
public sealed class JobTrackingSystem : SharedJobTrackingSystem
{
    [Dependency] private readonly IAfkManager _afk = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly ColcommJobSystem _colcommJobs = default!; // HardLight

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JobTrackingComponent, CryosleepBeforeMindRemovedEvent>(OnJobBeforeCryoEntered);
        SubscribeLocalEvent<JobTrackingComponent, MindAddedMessage>(OnJobMindAdded);
        SubscribeLocalEvent<JobTrackingComponent, MindRemovedMessage>(OnJobMindRemoved);
        SubscribeLocalEvent<ColcommRegistryRoundStartEvent>(OnColcommRegistryRoundStart); // HardLight
        SubscribeLocalEvent<StationJobsComponent, EntityTerminatingEvent>(OnStationJobsTerminating); // HardLight
    }

    /// <summary>
    /// HardLight: When a station is destroyed mid-round (ship sold/destroyed), release any
    /// ColComm slots still attributed to crew whose <see cref="JobTrackingComponent.SpawnStation"/>
    /// pointed at it. Without this, players whose body survives station deletion (e.g. transferred
    /// to another grid, ghosted later, or in a shuttle that left) keep their ColComm slot held
    /// until their body itself dies/cryos. Marks each tracked component inactive so its later
    /// MindRemovedMessage does not double-refund.
    /// </summary>
    private void OnStationJobsTerminating(Entity<StationJobsComponent> ent, ref EntityTerminatingEvent args)
    {
        var query = AllEntityQuery<JobTrackingComponent>();
        while (query.MoveNext(out var bodyUid, out var tracking))
        {
            if (!tracking.Active || tracking.SpawnStation != ent.Owner)
                continue;

            OpenJob((bodyUid, tracking));
        }
    }

    /// <summary>
    /// HardLight: After the ColComm registry resets to defaults, deduct slots for all crew
    /// that persisted from the previous round (Active = true in their JobTrackingComponent).
    /// </summary>
    private void OnColcommRegistryRoundStart(ColcommRegistryRoundStartEvent ev)
    {
        var activeCounts = new Dictionary<ProtoId<JobPrototype>, int>();

        var jobQuery = AllEntityQuery<JobTrackingComponent>();
        while (jobQuery.MoveNext(out _, out var job))
        {
            if (!job.Active || job.Job is not { } jobId)
                continue;

            var colcommJobId = _stationJobs.GetColcommJobId(jobId);
            activeCounts.TryGetValue(colcommJobId, out var existing);
            activeCounts[colcommJobId] = existing + 1;
        }

        if (activeCounts.Count > 0)
            _colcommJobs.DeductActiveRoles(ev.Colcomm, activeCounts);
    }

    // HardLight: If a player returns to their body (or an admin forces a mind in), consume a
    // ColComm slot unless we already track them.
    private void OnJobMindAdded(Entity<JobTrackingComponent> ent, ref MindAddedMessage ev)
    {
        // If the job is null, don't do anything.
        if (ent.Comp.Job is not { } job)
            return;

        if (!ent.Comp.Active)
        {
            ent.Comp.Active = true;
            RaiseLocalEvent(new JobTrackingStateChangedEvent());
        }

        if (!ShouldReopenTrackedJob(ent.Comp.SpawnStation, job))
            return;

        if (!_player.TryGetSessionByEntity(ent, out var session))
            return;

        CloseJob(ent, session.UserId);
    }

    private void OnJobMindRemoved(Entity<JobTrackingComponent> ent, ref MindRemovedMessage ev)
    {
        if (ent.Comp.Job == null || !ent.Comp.Active || !ShouldReopenTrackedJob(ent.Comp.SpawnStation, ent.Comp.Job.Value))
            return;

        OpenJob(ent, ev.Mind.Comp.UserId); // HardLight: Added ev.Mind.Comp.UserId
    }

    private void OnJobBeforeCryoEntered(Entity<JobTrackingComponent> ent, ref CryosleepBeforeMindRemovedEvent ev)
    {
        if (ent.Comp.Job == null || !ent.Comp.Active || !ShouldReopenTrackedJob(ent.Comp.SpawnStation, ent.Comp.Job.Value))
            return;

        OpenJob(ent, ev.User); // HardLight: Added ev.User
    }

    public void OpenJob(Entity<JobTrackingComponent> ent, NetUserId? userId = null) // HardLight: Added NetUserId? userId = null
    {
        if (ent.Comp.Job is not { } job)
            return;

        // HardLight: idempotent — if the slot was already opened (e.g. by station deletion sweep
        // or a prior disconnect grace release), don't refund again.
        if (!ent.Comp.Active)
            return;

        ent.Comp.Active = false;
        RaiseLocalEvent(new JobTrackingStateChangedEvent()); // HardLight

        TryComp<StationJobsComponent>(ent.Comp.SpawnStation, out var stationJobs);
        var stationJob = _stationJobs.GetStationTrackingJobId(ent.Comp.SpawnStation, job, stationJobs);

        NetUserId? trackedUserId = userId;
        if (trackedUserId == null && _player.TryGetSessionByEntity(ent, out var session))
            trackedUserId = session.UserId;

        if (trackedUserId != null && stationJobs != null)
            _stationJobs.TryUntrackPlayerJob(ent.Comp.SpawnStation, trackedUserId.Value, stationJob, stationJobs);

        if (stationJobs != null)
            _stationJobs.TryReopenTrackedJobSlot(ent.Comp.SpawnStation, stationJob, stationJobs);

        var colcommJob = _stationJobs.GetColcommJobId(job);

        if (_colcommJobs.TryGetColcommRegistry(out var colcomm)
            && _colcommJobs.TryGetJobSlot(colcomm, colcommJob, out var slots)
            && slots != null)
        {
            // Only reopen the global pool if it has spare capacity for this role.
            var occupiedJobs = GetNumberOfActiveColcommRoles(colcommJob, includeAfk: true, exclude: ent, includeOutsideDefaultMap: true);
            var midRoundMax = colcomm.Comp.MidRoundMaxSlots.GetValueOrDefault(colcommJob, 0);

            if (slots + occupiedJobs < midRoundMax)
                _colcommJobs.TryAdjustJobSlot(colcomm, colcommJob, 1);

            if (trackedUserId != null)
                _colcommJobs.TryUntrackPlayerJob(colcomm, trackedUserId.Value, colcommJob);
        }
    }

    public void EnsureTrackedJob(EntityUid uid, ProtoId<JobPrototype> jobId, EntityUid spawnStation, bool active = true)
    {
        var jobComp = EnsureComp<JobTrackingComponent>(uid);
        jobComp.Job = jobId;
        jobComp.SpawnStation = spawnStation;
        jobComp.Active = active;
        Dirty(uid, jobComp);
    }

    // HardLight: CloseJob consumes a reopened slot and re-tracks the player in ColComm/station job registries.
    private void CloseJob(Entity<JobTrackingComponent> ent, NetUserId userId)
    {
        if (ent.Comp.Job is not { } job)
            return;

        if (!ent.Comp.Active)
        {
            ent.Comp.Active = true;
            RaiseLocalEvent(new JobTrackingStateChangedEvent());
        }

        if (!ShouldReopenTrackedJob(ent.Comp.SpawnStation, job))
            return;

        var stationJob = _stationJobs.GetStationTrackingJobId(ent.Comp.SpawnStation, job);

        if (TryComp<StationJobsComponent>(ent.Comp.SpawnStation, out var stationJobs)
            && !_stationJobs.IsPlayerJobTracked(ent.Comp.SpawnStation, userId, stationJob, stationJobs))
        {
            if (_stationJobs.TryGetJobSlot(ent.Comp.SpawnStation, stationJob, out var localSlots) && localSlots > 0)
                _stationJobs.TryAdjustJobSlot(ent.Comp.SpawnStation, stationJob, -1, clamp: true, stationJobs: stationJobs);

            _stationJobs.TryTrackPlayerJob(ent.Comp.SpawnStation, userId, stationJob, stationJobs);
        }

        var colcommJob = _stationJobs.GetColcommJobId(job);

        if (!_colcommJobs.TryGetColcommRegistry(out var colcomm)
            || !_colcommJobs.TryGetJobSlot(colcomm, colcommJob, out var slots)
            || _colcommJobs.IsPlayerJobTracked(colcomm, userId, colcommJob))
        {
            return;
        }

        if (slots > 0)
            _colcommJobs.TryAdjustJobSlot(colcomm, colcommJob, -1, clamp: true);

        _colcommJobs.TryTrackPlayerJob(colcomm, userId, colcommJob);
    }

    /// <summary>
    /// VRS: Move a <see cref="JobTrackingComponent"/> from one body to another so a subsequent
    /// mind transfer between them does not trigger the slot-reopen pathway. Used by the
    /// AI / borg shunt flow (<c>PositronicJumpSystem</c>) to fix HardLight issue #1354 where
    /// the AI job slot reopened to new joiners as soon as the AI shunted into a chassis.
    /// </summary>
    /// <remarks>
    /// The destination's <see cref="MindAddedMessage"/> still runs <see cref="OnJobMindAdded"/>
    /// and falls through to <see cref="CloseJob"/>, but <c>CloseJob</c>'s
    /// <c>IsPlayerJobTracked</c> guard short-circuits the actual slot decrement because the
    /// player is already tracked against this slot from their original spawn.
    /// </remarks>
    public void MoveTrackingFromTo(EntityUid from, EntityUid to)
    {
        if (!TryComp<JobTrackingComponent>(from, out var source))
            return;

        // Suppress OnJobMindRemoved on the source: it early-returns when Active is false.
        source.Active = false;

        var dest = EnsureComp<JobTrackingComponent>(to);
        dest.Job = source.Job;
        dest.SpawnStation = source.SpawnStation;
        // Mark Active so the destination's OnJobMindAdded skips the activation branch and
        // CloseJob reaches its IsPlayerJobTracked no-op path instead of trying to consume
        // a slot that was already consumed at the original spawn.
        dest.Active = true;

        RemComp<JobTrackingComponent>(from);
    }

    private bool ShouldReopenTrackedJob(EntityUid spawnStation, ProtoId<JobPrototype> job)
    {
        if (JobShouldBeReopened(job))
            return true;

        return _stationJobs.GetStationTrackingJobId(spawnStation, job) != job;
    }

    private int GetNumberOfActiveColcommRoles(
        ProtoId<JobPrototype> colcommJobId,
        bool includeAfk = true,
        EntityUid? exclude = null,
        bool includeOutsideDefaultMap = false)
    {
        var activeJobCount = 0;
        var jobQuery = AllEntityQuery<JobTrackingComponent, MindContainerComponent, TransformComponent>();
        while (jobQuery.MoveNext(out var uid, out var job, out _, out var xform))
        {
            if (exclude == uid)
                continue;

            if (!job.Active
                || job.Job is not { } trackedJob
                || _stationJobs.GetColcommJobId(trackedJob) != colcommJobId
                || (!includeOutsideDefaultMap && xform.MapID != _gameTicker.DefaultMap))
                continue;

            if (_player.TryGetSessionByEntity(uid, out var session))
            {
                if (session.State.Status != SessionStatus.InGame)
                    continue;

                if (!includeAfk && _afk.IsAfk(session))
                    continue;
            }

            activeJobCount++;
        }

        return activeJobCount;
    }

    /// <summary>
    /// Returns the number of active players who match the requested Job Prototype Id.
    /// </summary>
    // HardLight start
    public int GetNumberOfActiveRoles(
        ProtoId<JobPrototype> jobProtoId,
        bool includeAfk = true,
        EntityUid? exclude = null,
        bool includeOutsideDefaultMap = false)
    // HardLight end
    {
        var activeJobCount = 0;
        var jobQuery = AllEntityQuery<JobTrackingComponent, MindContainerComponent, TransformComponent>();
        while (jobQuery.MoveNext(out var uid, out var job, out _, out var xform)) // HardLight: out var mindContainer<out _
        {
            if (exclude == uid)
                continue;

            if (!job.Active
                || job.Job != jobProtoId
                || (!includeOutsideDefaultMap && xform.MapID != _gameTicker.DefaultMap)) // Skip if they're in cryo or on expedition, // HardLight: Added !includeOutsideDefaultMap
                continue;

            if (_player.TryGetSessionByEntity(uid, out var session))
            {
                if (session.State.Status != SessionStatus.InGame)
                    continue;

                if (!includeAfk && _afk.IsAfk(session))
                    continue;
            }

            activeJobCount++;
        }
        return activeJobCount;
    }
}

// HardLight: An event raised when a job tracking component's active state changes, used for dynamic job allocation rules.
public sealed class JobTrackingStateChangedEvent : EntityEventArgs
{
}
