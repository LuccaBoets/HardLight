using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Abilities.Psionics
{
    [RegisterComponent]
    public sealed partial class MassSleepPowerComponent : Component
    {
        [DataField("radius")]
        public float Radius = 3f;
        [DataField("massSleepActionId",
        customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
        public string? MassSleepActionId = "ActionMassSleep";

        [DataField("massSleepActionEntity")]
        public EntityUid? MassSleepActionEntity;
    }
}
