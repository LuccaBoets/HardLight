using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._HL.Brainwashing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class BrainwasherComponent : Component
{
    [DataField(serverOnly: true)]
    public DoAfterId? DoAfter;

    [DataField]
    public bool BypassMindshield;

    [DataField]
    public SoundSpecifier ChargingSound = new SoundPathSpecifier("/Audio/Effects/PowerSink/charge_fire.ogg");

    [DataField]
    public SoundSpecifier EngageSound = new SoundPathSpecifier("/Audio/Weapons/flash.ogg");

    [DataField]
    public TimeSpan ChardingDuration = TimeSpan.FromSeconds(3);

    [DataField, ViewVariables, AutoNetworkedField]
    public List<string> Compulsions = [];

    [DataField]
    public string ConfigureText = "Configure Neuralyzer";
}

public abstract class SharedBrainwasherSystem : EntitySystem;

[Serializable, NetSerializable]
public sealed partial class EngagedEvent : SimpleDoAfterEvent
{
    public EngagedEvent(NetEntity wearer)
    {
        Wearer = wearer;
    }
    [DataField]
    public NetEntity Wearer;
}

[Serializable, NetSerializable]
public sealed class BrainwashedEvent : EntityEventArgs;
