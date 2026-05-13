using Content.Server.Explosion.EntitySystems;
using Content.Shared._Starlight.NullSpace;
using Content.Shared._Starlight;
using Content.Shared.Stacks;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.NullSpace;

/// <summary>
/// Flashers: periodically scan for NullSpace entities within range and pulse to remove + stun.
/// Crystals: same effect but only on manual activation (TriggerOnUse/TriggerOnActivate).
/// Update-based detection is used for flashers because NullSpace entities cancel physics contacts,
/// making physics-based proximity sensors blind to them.
/// </summary>
public sealed class BluespacePulseOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    private const string BluespaceFlasherId = "BluespaceFlasher";
    private const string BluespaceCrystalId = "MaterialBluespace";

    private static readonly SoundPathSpecifier NullSpaceCutoffSound = new("/Audio/_HL/Effects/ma cutoff.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BluespacePulseOnTriggerComponent, TriggerEvent>(OnCrystalActivated);
    }

    private void OnCrystalActivated(Entity<BluespacePulseOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (MetaData(ent).EntityPrototype?.ID?.StartsWith(BluespaceCrystalId) != true)
            return;

        if (TryComp<StackComponent>(ent, out var stack))
        {
            _stack.Use(ent, 1, stack);
            if (stack.Count <= 0)
                QueueDel(ent);
        }

        // Do NOT call _trigger.Trigger(uid) here — TriggerOnUse/TriggerOnActivate already fired TriggerEvent
        // to get us here, so calling Trigger again would cause infinite recursion.
        DoPulse(ent, ent.Comp);
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<BluespacePulseOnTriggerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (curTime < comp.NextTrigger)
                continue;

            if (MetaData(uid).EntityPrototype?.ID != BluespaceFlasherId)
                continue;

            if (!xform.Anchored)
                continue;

            var found = new List<EntityUid>();
            foreach (var ent in _lookup.GetEntitiesInRange(uid, comp.Radius))
            {
                if (HasComp<NullSpaceComponent>(ent))
                    found.Add(ent);
            }

            if (found.Count == 0)
                continue;

            comp.NextTrigger = curTime + comp.Cooldown;
            _trigger.Trigger(uid);
            DoPulse(uid, comp, found);
        }
    }

    private void DoPulse(EntityUid uid, BluespacePulseOnTriggerComponent comp, List<EntityUid>? preFound = null)
    {
        var found = preFound ?? new List<EntityUid>();
        if (preFound == null)
        {
            foreach (var ent in _lookup.GetEntitiesInRange(uid, comp.Radius))
            {
                if (HasComp<NullSpaceComponent>(ent))
                    found.Add(ent);
            }
        }

        var stunTime = TimeSpan.FromSeconds(comp.StunSeconds);
        foreach (var ent in found)
        {
            if (HasComp<ShadekinComponent>(ent))
                _audio.PlayPvs(NullSpaceCutoffSound, ent);
            RemComp<NullSpaceComponent>(ent);
            _stun.TryParalyze(ent, stunTime, true);
        }
    }
}
