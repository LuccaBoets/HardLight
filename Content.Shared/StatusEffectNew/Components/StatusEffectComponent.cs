using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.StatusEffectNew.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class StatusEffectComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? AppliedTo;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan StartEffectTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan? EndEffectTime;

    [ViewVariables]
    public TimeSpan Duration => EndEffectTime == null ? TimeSpan.MaxValue : EndEffectTime.Value - StartEffectTime;
}
