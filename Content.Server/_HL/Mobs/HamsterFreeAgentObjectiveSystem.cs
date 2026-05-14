using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Tag;

namespace Content.Server._HL.Mobs;

/// <summary>
/// Gives sentient hamsters their one fixed free-agent objective when a ghost takes them over.
/// </summary>
public sealed class HamsterFreeAgentObjectiveSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private const string HamsterTag = "Hamster";
    private const string FreeAgentRole = "FreeAgent";
    private const string HamsterObjective = "HamsterGloriousDeathObjective";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
    }

    private void OnRoleAdded(RoleAddedEvent args)
    {
        if (args.Mind.RoleType != FreeAgentRole)
            return;

        if (args.Mind.OwnedEntity is not { } owned)
            return;

        if (!_tag.HasTag(owned, HamsterTag))
            return;

        _mind.TryAddObjective(args.MindId, args.Mind, HamsterObjective);
    }
}