using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared.Chemistry.Prototypes;

/// <summary>
/// Defines behavior presets for injector-based tools.
/// </summary>
[Prototype("injectorMode")]
public sealed partial class InjectorModePrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<InjectorModePrototype>))]
    public string[]? Parents { get; private set; }

    [AbstractDataField, NeverPushInheritance]
    public bool Abstract { get; private set; }

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public bool InjectOnUse;

    [DataField]
    public List<FixedPoint2> TransferAmounts = new() { 1, 5, 10, 15 };

    [DataField]
    public TimeSpan MobTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan ContainerDrawTime = TimeSpan.Zero;

    [DataField]
    public float DownedModifier = 0.5f;

    [DataField]
    public float SelfModifier = 0.5f;

    [DataField]
    public TimeSpan DelayPerVolume = TimeSpan.FromSeconds(0.1);

    [DataField]
    public FixedPoint2 IgnoreDelayForVolume = FixedPoint2.New(5);

    [DataField]
    public LocId PopupUserAttempt = "injector-component-injecting-user";

    [DataField]
    public LocId PopupTargetAttempt = "injector-component-injecting-target";

    [DataField]
    public InjectorBehavior Behavior = InjectorBehavior.Inject;

    [DataField]
    public SoundSpecifier? InjectSound;

    [DataField]
    public LocId? InjectPopupTarget;
}

[Serializable, NetSerializable, Flags]
public enum InjectorBehavior
{
    Inject = 1 << 0,
    Draw = 1 << 1,
    Dynamic = 1 << 2,
}

public static class InjectorBehaviorExtensions
{
    public static bool HasAnyFlag(this InjectorBehavior left, InjectorBehavior right)
    {
        return (left & right) != 0;
    }
}
