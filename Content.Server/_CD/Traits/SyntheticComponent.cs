namespace Content.Server._CD.Traits;

/// <summary>
/// Set players' blood to coolant, and is used to notify them of ion storms
/// </summary>
[RegisterComponent, Access(typeof(SyntheticSystem))] // HardLight: Synth<Synthetic
public sealed partial class SyntheticComponent : Component // HardLight: Synth<Synthetic
{
    /// <summary>
    /// The chance that the synthetic is alerted of an ion storm, // HardLight: synth<synthetic
    /// </summary>
    [DataField]
    public float AlertChance = 0.3f;
}
