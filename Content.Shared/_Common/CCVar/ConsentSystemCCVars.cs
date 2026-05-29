// SPDX-FileCopyrightText: Copyright (c) 2025 Space Wizards Federation
// SPDX-License-Identifier: MIT

namespace Content.Shared._Common.CCVar;

using Robust.Shared;
using Robust.Shared.Configuration;

[CVarDefs]
public sealed partial class ConsentSystemCCVars : CVars
{
    /// <summary>
    /// How many characters the consent text can be.
    /// </summary>
    public static readonly CVarDef<int> ConsentFreetextMaxLength =
        CVarDef.Create("consent.freetext_max_length", 1000, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// When true, the local client renders Non-Con role markers using a
    /// colorblind-safe palette (amber / teal-green / lavender) instead of the
    /// default (red / blue / purple). Shape cues (horns up / down / both)
    /// are unchanged. Purely a viewer-side preference: does not affect what
    /// other players see, and does not change which roles you have toggled.
    /// </summary>
    public static readonly CVarDef<bool> ConsentNonconColorblindPalette =
        CVarDef.Create("consent.noncon_colorblind_palette", false, CVar.ARCHIVE | CVar.CLIENTONLY);
}
