// SPDX-FileCopyrightText: 2026 Voidrift
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._VRS.CCVar;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._VRS.Sop;

/// <summary>
/// Auto-issues per-department Standard Operating Procedure books at player spawn.
/// </summary>
/// <remarks>
/// On <see cref="PlayerSpawnCompleteEvent"/> the system resolves the spawning job's
/// department, checks the matching <c>voidrift.sop.&lt;dept&gt;_enabled</c> cvar, and if
/// enabled spawns the configured book and inserts it into the player's PDA <c>SopSlot</c>.
/// If the player has no PDA (or the slot can't accept the item) the book is placed in a
/// free hand instead. If even that fails the book is deleted to avoid littering spawn points.
/// </remarks>
public sealed class SopIssuerSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    /// <summary>Department id -> SOP book entity prototype id to spawn.</summary>
    private static readonly Dictionary<string, string> DepartmentBooks = new()
    {
        { "Command",     "BookSopCommand" },
        { "Security",    "BookSopSecurity" },
        { "Medical",     "BookSopMedical" },
        { "Engineering", "BookSopEngineering" },
        { "Science",     "BookSopScience" },
        { "Cargo",       "BookSopCargo" },
        { "Service",     "BookSopService" },
        { "Civilian",    "BookSopCivilian" },
    };

    /// <summary>Department id -> cvar gating whether the book is issued.</summary>
    private static readonly Dictionary<string, CVarDef<bool>> DepartmentCvars = new()
    {
        { "Command",     SopCCVars.SopCommandEnabled },
        { "Security",    SopCCVars.SopSecurityEnabled },
        { "Medical",     SopCCVars.SopMedicalEnabled },
        { "Engineering", SopCCVars.SopEngineeringEnabled },
        { "Science",     SopCCVars.SopScienceEnabled },
        { "Cargo",       SopCCVars.SopCargoEnabled },
        { "Service",     SopCCVars.SopServiceEnabled },
        { "Civilian",    SopCCVars.SopCivilianEnabled },
    };

    /// <summary>Job prototype id -> primary department id.</summary>
    /// <remarks>
    /// Built lazily on first spawn from <see cref="DepartmentPrototype"/>. A job can appear in
    /// multiple departments (e.g. heads listed under both Command and their primary dept); we
    /// prefer the department whose key is in <see cref="DepartmentBooks"/> so heads still get
    /// their primary department's SOP rather than Command's.
    /// </remarks>
    private Dictionary<string, string>? _jobToDept;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs ev)
    {
        // Invalidate so the next spawn rebuilds against the new department graph.
        if (ev.WasModified<DepartmentPrototype>())
            _jobToDept = null;
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId is not { } jobId)
            return;

        var map = _jobToDept ??= BuildJobToDept();
        if (!map.TryGetValue(jobId, out var dept))
            return;

        if (!DepartmentCvars.TryGetValue(dept, out var cvar) || !_cfg.GetCVar(cvar))
            return;

        if (!DepartmentBooks.TryGetValue(dept, out var bookProto))
            return;

        IssueBook(ev.Mob, bookProto);
    }

    private void IssueBook(EntityUid mob, string bookProto)
    {
        var coords = Transform(mob).Coordinates;
        var book = Spawn(bookProto, coords);

        // 1. PDA SopSlot (preferred).
        if (_inventory.TryGetSlotEntity(mob, "id", out var idSlot)
            && TryComp<PdaComponent>(idSlot, out var pda)
            && _itemSlots.TryInsert(idSlot.Value, pda.SopSlot, book, user: null, excludeUserAudio: true))
        {
            return;
        }

        // 2. Free hand fallback.
        if (_hands.TryPickupAnyHand(mob, book, checkActionBlocker: false))
            return;

        // 3. Nothing took it - leave it on the floor so admins can spot the failure.
        // (Spawn already placed it at the mob's coordinates, so just return.)
    }

    private Dictionary<string, string> BuildJobToDept()
    {
        // Two-pass: first record every (job, dept) pairing, then for each job prefer a
        // department we actually have an SOP for (skips Command for heads with a primary dept).
        var pairs = new Dictionary<string, List<string>>();
        foreach (var dept in _proto.EnumeratePrototypes<DepartmentPrototype>())
        {
            foreach (var jobId in dept.Roles)
            {
                if (!pairs.TryGetValue(jobId, out var list))
                {
                    list = new List<string>();
                    pairs[jobId] = list;
                }
                list.Add(dept.ID);
            }
        }

        var map = new Dictionary<string, string>(pairs.Count);
        foreach (var (job, depts) in pairs)
        {
            string? pick = null;
            // Prefer non-Command, SOP-supported dept (heads).
            foreach (var d in depts)
            {
                if (d == "Command")
                    continue;
                if (DepartmentBooks.ContainsKey(d))
                {
                    pick = d;
                    break;
                }
            }
            // Fall back to any SOP-supported dept (including Command).
            if (pick is null)
            {
                foreach (var d in depts)
                {
                    if (DepartmentBooks.ContainsKey(d))
                    {
                        pick = d;
                        break;
                    }
                }
            }
            if (pick is not null)
                map[job] = pick;
        }
        return map;
    }
}
