using System.Numerics;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Weather;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedWeatherSystem))]
public sealed partial class WeatherStatusEffectComponent : Component
{
    [DataField(required: true)]
    public SpriteSpecifier Sprite = default!;

    [DataField]
    public Color? Color;

    [DataField]
    public Vector2? Scrolling;

    [DataField]
    public SoundSpecifier? Sound;

    [ViewVariables]
    public EntityUid? Stream;
}
