using Content.Shared.NPC.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.NullSpace;

[RegisterComponent, NetworkedComponent]
public sealed partial class NullSpaceComponent : Component
{
    public List<ProtoId<NpcFactionPrototype>> SuppressedFactions = new();

    // Component types that were already on the entity before null phase entry;
    // these are not removed on exit so genetics-granted components are preserved.
    public HashSet<Type> PreExistingComponents = new();
}