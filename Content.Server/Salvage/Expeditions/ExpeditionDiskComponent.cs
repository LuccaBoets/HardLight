using Content.Shared.Salvage;
using Content.Shared.Salvage.Expeditions;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Salvage.Expeditions;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ExpeditionDiskComponent : Component
{
    [DataField("difficulty")]
    public string Difficulty = "NFEasy";

    [DataField("difficultyNumber")]
    public int DifficultyNumber = 1;

    [DataField("seed")]
    public int Seed = 0;

    [DataField("missionType")]
    public SalvageMissionType MissionType = SalvageMissionType.Destruction;

    [ViewVariables(VVAccess.ReadWrite), DataField("cooldownEnd", customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan CooldownEnd = TimeSpan.Zero;
}
