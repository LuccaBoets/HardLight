namespace Content.Server._HL.Save;

/// <summary>
/// Shared filtering rules for the room save format (ShipGridData / custom serializer path).
/// Ship saves use a separate YAML-level sanitizer (<see cref="Content.Server._HL.Shipyard.ShipSaveYamlSanitizer"/>);
/// room saves apply equivalent rules at the ECS level inside SerializeShipArea.
/// </summary>
public static class RoomSaveRules
{
    /// <summary>
    /// Entity prototype IDs that must never appear in room saves.
    /// Mirrors the spirit of ShipSaveYamlSanitizer.FilteredPrototypes for the room context.
    /// </summary>
    public static readonly IReadOnlySet<string> FilteredPrototypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ContainmentField",
        "PortalBlue",
        "PortalRed",
        "ShipShield",
    };

    /// <summary>
    /// Component type names (without namespace) that, when present on an entity, cause that
    /// entity to be excluded from the room save entirely.
    /// Mirrors ShipSaveYamlSanitizer.FilteredEntityByComponentTypes for the room context.
    /// </summary>
    public static readonly IReadOnlySet<string> EntityExclusionComponentNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "GhostComponent",
        "GhostRoleComponent",
        "HumanoidAppearanceComponent",
        "MindContainerComponent",
        "MobStateComponent",
    };

    /// <summary>
    /// Component type names that should be stripped from entity data during a room save.
    /// Mirrors ShipSaveYamlSanitizer.FilteredTypes for the room context.
    /// These are resolved at the ECS level via IsProblematicComponent in ShipSerializationSystem.
    /// </summary>
    public static readonly IReadOnlySet<string> FilteredComponentNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "JointComponent",
        "StationMemberComponent",
        "NavMapComponent",
        "ShuttleDeedComponent",
        "IFFComponent",
        "LinkedLifecycleGridParentComponent",
        "DeviceNetworkComponent",
        "UserInterfaceComponent",
        "DockingComponent",
        "ActionGrantComponent",
        "MindComponent",
        "MindContainerComponent",
        "ForensicsComponent",
        "ContainmentFieldGeneratorComponent",
        "ActionsComponent",
        "ProjectileComponent",
        "ItemToggleActiveSoundComponent",
        "BlockingComponent",
        "TurnstileComponent",
        "SubdermalImplantComponent",
    };
}
