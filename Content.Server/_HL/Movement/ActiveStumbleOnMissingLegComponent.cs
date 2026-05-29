namespace Content.Server._HL.Movement;

/// <summary>
/// Server-side marker added by <see cref="StumbleOnMissingLegSystem"/> only
/// while an entity is both player-controlled AND has a partial leg count.
/// Iterating this marker keeps the per-tick query small instead of scanning
/// every humanoid each frame.
/// </summary>
[RegisterComponent]
public sealed partial class ActiveStumbleOnMissingLegComponent : Component;
