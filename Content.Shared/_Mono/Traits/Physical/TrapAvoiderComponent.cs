// SPDX-FileCopyrightText: 2025 ark1368
//
// SPDX-License-Identifier: MPL-2.0

using Robust.Shared.GameStates; // HardLight

namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Step triggers will not activate when this entity steps on them.
/// </summary>
[RegisterComponent, NetworkedComponent] // HardLight: Added NetworkedComponent
public sealed partial class TrapAvoiderComponent : Component;
