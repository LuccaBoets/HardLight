namespace Content.Server._NF.Security.Components;

[RegisterComponent]
public sealed partial class DisableCdetOnAlertLevelComponent : Component
{
    [DataField]
    public HashSet<string> EnabledAlertLevels =
    [
        "green",
        "blue",
        "yellow",
        "white",
    ];

    [DataField]
    public HashSet<string> DisabledAlertLevels =
    [
        "red",
        "violet",
        "gamma",
        "delta",
        "epsilon",
        "omicron",
    ];

    [ViewVariables]
    public bool RestoreAfterLockdown;
}