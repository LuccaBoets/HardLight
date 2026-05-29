using Content.Server.Body.Components;
using Content.Shared.Body.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server.Floofstation.Traits.Components;

/// <summary>
///     Enables the mob to suck blood from other mobs to replenish its own saturation.
///     Must be fully initialized before being added to a mob.
/// </summary>
[RegisterComponent]
public sealed partial class VampirismComponent : Component
{
    [DataField]
    public HashSet<ProtoId<MetabolizerTypePrototype>> MetabolizerPrototypes = new() { "Vampiric", "Animal" };

    [DataField]
    public List<MetabolismGroupEntry> AddedMetabolismGroups = new(), RemovedMetabolismGroups = new();


    /// <summary>
    ///     A whitelist for what special-digestible-required foods the vampire's stomach is capable of eating.
    /// </summary>
    [DataField]
    public EntityWhitelist? SpecialDigestible = null;

    [DataField]
    public bool IsSpecialDigestibleExclusive = true;

    [DataField]
    public TimeSpan SuccDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public float UnitsToSucc = 10; //

    [DataField("overfedWalkMultiplier")]
    public float OverfedWalkMultiplier = 1f;

    [DataField("overfedSprintMultiplier")]
    public float OverfedSprintMultiplier = 1f;

    [DataField("peckishWalkMultiplier")]
    public float PeckishWalkMultiplier = 1f;

    [DataField("peckishSprintMultiplier")]
    public float PeckishSprintMultiplier = 1f;

    [DataField("starvingWalkMultiplier")]
    public float StarvingWalkMultiplier = 1f;

    [DataField("starvingSprintMultiplier")]
    public float StarvingSprintMultiplier = 1f;

    /// <summary>
    ///     Passive health regeneration when overfed (damage healed per second).
    /// </summary>
    [DataField("overfedRegenAmount")]
    public float OverfedRegenAmount = 0.5f;

    /// <summary>
    ///     Damage multiplier for unarmed attacks.
    /// </summary>
    [DataField("unarmedDamageMultiplier")]
    public float UnarmedDamageMultiplier = 1.3f;

    /// <summary>
    ///     Damage multiplier when starving (vulnerability).
    /// </summary>
    [DataField("starvingDamageMultiplier")]
    public float StarvingDamageMultiplier = 1.5f;
}
