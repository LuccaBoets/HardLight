using Content.Shared.Verbs;
using Content.Shared.Animals.Components;
using Robust.Shared.Player;

namespace Content.Shared.Animals.Systems;

/*
    HL
    Moved LewdEggLayingSystem to a shared system so that the AddVerb can be called clientside to deal with lag when right-clicking objects/players.
    LewdEggLayingComponent is now shared, and we had to network the eggs count.
    We also had to move the AddEgg to a helper function to keep any processing code out of the Component and in the Shared space
    The LewdEggLayingSystem on the Server is mostly handling everything, with an empty class of the same name on the Client so that the client can run the AddVerb code locally.
*/
public abstract class SharedPrizeballLayingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrizeballLayingComponent, GetVerbsEvent<InnateVerb>>(AddLayPballInsideVerb);
    }

    private void AddLayPballInsideVerb(Entity<PrizeballLayingComponent> user, ref GetVerbsEvent<InnateVerb> args)
    {
        // Todo figure out how to only make verb appear for player mobs
        var target = args.Target;
        if (!args.CanInteract || user.Owner == target || !user.Comp.hasPballs() || !TryComp(target, out ActorComponent? actor))
            return;

        InnateVerb verbLayPball = new()
        {
            Act = () => AttemptLayInside(user, target),
            Text = Loc.GetString($"lay-pball-inside-verb-get-text"),
            Priority = 1
        };
        args.Verbs.Add(verbLayPball);
    }

    protected void AddPballs(EntityUid uid, PrizeballLayingComponent comp, float amt)
    {
        comp.pballs = Math.Clamp(comp.pballs + amt, 0, comp.MaxPballs);
        if (amt > 0)
        {
            comp.pballsFlavorAccum += amt;
        }
        DirtyField(uid, comp, nameof(PrizeballLayingComponent.pballs));
    }

    protected virtual void AttemptLayInside(Entity<PrizeballLayingComponent> user, EntityUid target) { }

}