// SPDX-FileCopyrightText: 2026 Voidrift
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._VRS.CCVar;

/// <summary>
/// Per-department toggles for the SOP auto-issuer (SopIssuerSystem).
/// Each cvar gates whether the matching department's SOP book is spawned and inserted
/// into the player's PDA <c>SopSlot</c> (or hand) when they spawn for a job in that department.
/// All default to <c>false</c> so half-finished SOP content stays invisible in-game.
/// </summary>
[CVarDefs]
public sealed class SopCCVars
{
    public static readonly CVarDef<bool> SopCommandEnabled =
        CVarDef.Create("voidrift.sop.command_enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> SopSecurityEnabled =
        CVarDef.Create("voidrift.sop.security_enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> SopMedicalEnabled =
        CVarDef.Create("voidrift.sop.medical_enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> SopEngineeringEnabled =
        CVarDef.Create("voidrift.sop.engineering_enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> SopScienceEnabled =
        CVarDef.Create("voidrift.sop.science_enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> SopCargoEnabled =
        CVarDef.Create("voidrift.sop.cargo_enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> SopServiceEnabled =
        CVarDef.Create("voidrift.sop.service_enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> SopCivilianEnabled =
        CVarDef.Create("voidrift.sop.civilian_enabled", false, CVar.SERVERONLY);
}
