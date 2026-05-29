using System;
using Robust.Shared.Audio;

namespace Content.Server._NF.Security.Components;

[RegisterComponent]
public sealed partial class CdetReactiveLethalComponent : Component
{
    [DataField]
    public float AlertRadius = 5f;

    [DataField]
    public float AlertDuration = 5f;

    [DataField]
    public int NormalFireMode;

    [DataField]
    public int LethalFireMode = 1;

    [DataField(required: true)]
    public SoundSpecifier NonLethalSound = default!;

    [DataField(required: true)]
    public SoundSpecifier LethalSound = default!;

    [ViewVariables]
    public TimeSpan? AlertEndTime;
}