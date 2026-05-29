using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;

namespace Content.Shared._HL.Aphrodisiac;

public abstract class SharedAphrodisiacSystem : EntitySystem
{
    public static readonly ProtoId<StatusEffectPrototype> AphrodisiacKey = "Aphrodisiac";

    /* I have no clue why this magic number was chosen, I copied it from slur system and needed it for the overlay
    If you have a more intelligent magic number be my guest to completely explode this value.
    There were no comments as to why this value was chosen three years ago. */
    public static float MagicNumber = 1100f;

    [Dependency] protected readonly StatusEffectsSystem Status = default!;

    public override void Initialize()
    {
    }

    public void TryApplyAphrodisiacs(EntityUid uid, TimeSpan aphrodisiacPower, StatusEffectsComponent? status = null)
    {
        if (!Resolve(uid, ref status, false))
            return;

        var ev = new AphrodisiacEvent(aphrodisiacPower);
        RaiseLocalEvent(uid, ref ev);

        if (!Status.HasStatusEffect(uid, AphrodisiacKey, status))
            Status.TryAddStatusEffect<AphrodisiacStatusEffectComponent>(uid, AphrodisiacKey, ev.Duration, true, status);
        else
            Status.TrySetTime(uid, AphrodisiacKey, ev.Duration, status);
    }

    public void TryRemoveAphrodisiacs(EntityUid uid)
    {
        Status.TryRemoveStatusEffect(uid, AphrodisiacKey);
    }

    public void TryRemoveAphrodisiacsTime(EntityUid uid, TimeSpan aphrodisiacPower)
    {
        Status.TryRemoveTime(uid, AphrodisiacKey, aphrodisiacPower);
    }

    [ByRefEvent]
    public record struct AphrodisiacEvent(TimeSpan Duration);
}