using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Duration for missions
    /// </summary>
    public static readonly CVarDef<float>
        SalvageExpeditionDuration = CVarDef.Create("salvage.expedition_duration", 900f, CVar.REPLICATED); // Frontier: This is not used, look for SalvageTimeMod

    /// <summary>
    ///     Cooldown for missions.
    /// </summary>
    public static readonly CVarDef<float>
        SalvageExpeditionCooldown = CVarDef.Create("salvage.expedition_cooldown", 300f, CVar.REPLICATED);

    /// <summary>
    /// VRS: per-extra-crew payout multiplier on expedition completion. A solo run pays 1x; with this CVar at
    /// 0.25 a 2-player crew pays 1.25x, a 3-player crew pays 1.5x, capped by
    /// <see cref="SalvageExpeditionGroupBonusMax"/>. Applied by spawning round(multiplier) copies of the reward
    /// entity. Set to 0 to disable the group bonus entirely.
    /// </summary>
    public static readonly CVarDef<float> SalvageExpeditionGroupBonusPerPlayer =
        CVarDef.Create("salvage.expedition_group_bonus_per_player", 0.25f, CVar.SERVERONLY);

    /// <summary>
    /// VRS: hard cap on the group bonus multiplier from <see cref="SalvageExpeditionGroupBonusPerPlayer"/>.
    /// Default 2.5 means a large crew tops out at 2-3 reward briefcases per expedition regardless of size.
    /// </summary>
    public static readonly CVarDef<float> SalvageExpeditionGroupBonusMax =
        CVarDef.Create("salvage.expedition_group_bonus_max", 2.5f, CVar.SERVERONLY);

    /// <summary>
    /// VRS: comma-separated per-tier mob-budget multipliers applied to expedition spawn difficulty.
    /// Order is T1,T2,T3,T4,T5 (NFEasy, NFModerate, NFHazardous, NFExtreme, NFNightmare). Higher values
    /// spawn more mobs, making higher tiers prohibitively hard to clear solo and pushing groups toward
    /// T3+ expeditions. Default "1.0,1.0,1.2,1.5,2.0" keeps T1/T2 friendly for onboarding.
    /// Missing or malformed entries fall back to 1.0 for that tier.
    /// </summary>
    public static readonly CVarDef<string> SalvageExpeditionTierDifficulty =
        CVarDef.Create("salvage.expedition_tier_difficulty", "1.0,1.0,1.2,1.5,2.0", CVar.SERVERONLY);
}
