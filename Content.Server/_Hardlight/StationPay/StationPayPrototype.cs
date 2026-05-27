using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Hardlight.StationPay;

[Prototype]
public sealed partial class StationPayPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Spesos paid per hour to players with this job.
    /// </summary>
    [DataField("payPerHour", required: true, serverOnly: true)]
    public int PayPerHour;

    /// <summary>
    /// Job player must have to receive automated payments.
    /// </summary>
    [DataField("job", required: true, serverOnly: true)]
    public ProtoId<JobPrototype> JobProto;

    /// <summary>
    /// VRS: if true, this job does not receive the understaffed scarcity bonus.
    /// Used for roleplay-only jobs (clown, mime, chaplain, etc.) so that mechanically
    /// unimportant roles aren't artificially incentivised when no-one is playing them.
    /// </summary>
    [DataField("excludeFromScarcityBonus", serverOnly: true)]
    public bool ExcludeFromScarcityBonus;
}

