using Content.Shared.Abilities.Psionics;
using Content.Shared.Nyanotrasen.Abilities.Psionics;
using Content.Server.Electrocution;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Actions.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.Abilities.Psionics
{
    public sealed class NoosphericZapPowerSystem : EntitySystem
    {
        private static readonly EntProtoId NoosphericZapGunPrototype = "NoosphericZapGun";

        [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
        [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
        [Dependency] private readonly GunSystem _gun = default!;


        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<NoosphericZapPowerActionEvent>(OnPowerUsed);
        }

        private void OnPowerUsed(NoosphericZapPowerActionEvent args)
        {
            if (!_psionics.OnAttemptPowerUse(args.Performer, "noospheric zap"))
                return;

            var performerCoords = Comp<TransformComponent>(args.Performer).Coordinates;
            var targetCoords = Comp<TransformComponent>(args.Target).Coordinates;

            var zapGun = Spawn(NoosphericZapGunPrototype, performerCoords);
            if (TryComp<GunComponent>(zapGun, out var gunComp))
                _gun.AttemptShoot(args.Performer, zapGun, gunComp, targetCoords, args.Target);

            QueueDel(zapGun);

            _electrocution.TryDoElectrocution(args.Target, args.Performer, 1, TimeSpan.FromSeconds(3), refresh: true, ignoreInsulation: true);

            _psionics.LogPowerUsed(args.Performer, "noospheric zap");
            args.Handled = true;
        }
    }
}
