namespace Content.Shared._Starlight.NullSpace;

[RegisterComponent]
public sealed partial class NullPhaseComponent : Component
{
    [DataField]
    public EntityUid? PhaseAction;

    /// <summary>
    /// Cooldown in seconds applied after a voluntary exit from nullspace.
    /// </summary>
    [DataField]
    public float ExitDelay = 1f;

    /// <summary>
    /// Cooldown in seconds applied when the entity is forcibly ejected from nullspace.
    /// </summary>
    [DataField]
    public float ForcedEjectionPenalty = 60f;

    /// <summary>
    /// Set to true immediately before a voluntary exit so the shutdown handler can distinguish it from forced ejection.
    /// </summary>
    public bool VoluntaryExit;
}