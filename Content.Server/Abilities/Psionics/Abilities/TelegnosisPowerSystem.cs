using Content.Shared.Abilities.Psionics;
using Content.Shared.Nyanotrasen.Abilities.Psionics;
using Content.Shared.Mind.Components;
using Content.Shared.Actions.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Abilities.Psionics
{
    public sealed class TelegnosisPowerSystem : EntitySystem
    {
        private static readonly TimeSpan ProjectionDuration = TimeSpan.FromSeconds(30);

        [Dependency] private readonly MindSwapPowerSystem _mindSwap = default!;
        [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
        [Dependency] private readonly TransformSystem _transform = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<TelegnosisPowerComponent, TelegnosisPowerActionEvent>(OnPowerUsed);
            SubscribeLocalEvent<TelegnosticProjectionComponent, MindRemovedMessage>(OnMindRemoved);
        }

        private void OnPowerUsed(EntityUid uid, TelegnosisPowerComponent component, TelegnosisPowerActionEvent args)
        {
            if (!_psionics.OnAttemptPowerUse(args.Performer, "telegnosis"))
                return;

            var projection = Spawn(component.Prototype, Transform(uid).Coordinates);
            _transform.AttachToGridOrMap(projection);
            _mindSwap.Swap(uid, projection);
            _mindSwap.EnsureReturnAction(projection);
            Timer.Spawn(ProjectionDuration, () => ExpireProjection(projection));

            _psionics.LogPowerUsed(uid, "telegnosis");
            args.Handled = true;
        }

        private void ExpireProjection(EntityUid projection)
        {
            if (Deleted(projection))
                return;

            if (TryComp<MindSwappedComponent>(projection, out var swapped)
                && swapped.OriginalEntity.IsValid()
                && !Deleted(swapped.OriginalEntity)
                && HasComp<MindSwappedComponent>(swapped.OriginalEntity))
            {
                _mindSwap.Swap(projection, swapped.OriginalEntity, true);
                return;
            }

            QueueDel(projection);
        }

        private void OnMindRemoved(EntityUid uid, TelegnosticProjectionComponent component, MindRemovedMessage args)
        {
            QueueDel(uid);
        }
    }
}
