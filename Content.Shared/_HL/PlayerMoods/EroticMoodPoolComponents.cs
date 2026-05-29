namespace Content.Shared._HL.PlayerMoods;

/// <summary>
/// Grants vanilla erotic mood prompts drawn from common sexual preferences:
/// positions, acts, dynamics, and species attraction.
/// </summary>
[RegisterComponent]
public sealed partial class VanillaEroticMoodsComponent : EroticMoodPoolComponentBase
{
    public override string DatasetId => "EroticMoodsVanilla";
    public override string CategoryLocKey => "erotic-moods-category-vanilla";
}

/// <summary>
/// Grants kinky erotic mood prompts drawn from specific fetish and kink dynamics,
/// with consent-gating for mechanics like vore, mind control, and CnC.
/// </summary>
[RegisterComponent]
public sealed partial class KinkyEroticMoodsComponent : EroticMoodPoolComponentBase
{
    public override string DatasetId => "EroticMoodsKinky";
    public override string CategoryLocKey => "erotic-moods-category-kinky";
}

/// <summary>
/// Grants romantic mood prompts: tenderness, dates, closeness,
/// and the sweet non-sexual side of desire.
/// </summary>
[RegisterComponent]
public sealed partial class RomanticMoodsComponent : EroticMoodPoolComponentBase
{
    public override string DatasetId => "EroticMoodsRomantic";
    public override string CategoryLocKey => "erotic-moods-category-romantic";
}
