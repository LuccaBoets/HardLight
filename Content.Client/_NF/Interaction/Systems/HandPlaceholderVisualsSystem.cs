using Content.Client._NF.Hands.UI;
using Content.Client.Items;
using Content.Client.Items.Systems;
using Content.Shared._NF.Interaction.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client._NF.Interaction.Systems;

/// <summary>
/// Handles interactions with items that spawn HandPlaceholder items.
/// </summary>
[UsedImplicitly]
public sealed partial class HandPlaceholderVisualsSystem : EntitySystem
{
    [Dependency] ContainerSystem _container = default!;
    [Dependency] ItemSystem _item = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandPlaceholderComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);

        SubscribeLocalEvent<HandPlaceholderVisualsComponent, ComponentRemove>(PlaceholderRemove);

        Subs.ItemStatus<HandPlaceholderVisualsComponent>(_ => new HandPlaceholderStatus());
    }

    private void OnAfterAutoHandleState(Entity<HandPlaceholderComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp(ent, out HandPlaceholderVisualsComponent? placeholder))
            return;

        // HardLight #1236: only (re)spawn the dummy when we don't already have one.
        // The HandsUIController may eagerly spawn a dummy itself if this state event
        // arrives after the placeholder lands in the local player's hand. Replacing
        // the dummy here would invalidate the entity reference the hand button is
        // already pointing at and leave the slot empty until the module is reopened.
        if (placeholder.Dummy == EntityUid.Invalid)
            placeholder.Dummy = Spawn(ent.Comp.Prototype);

        if (_container.IsEntityInContainer(ent))
            _item.VisualsChanged(ent);
    }

    /// <summary>
    ///     HardLight #1236: ensure the placeholder has a valid dummy entity for visuals,
    ///     spawning one from the placeholder's prototype if it has not been created yet.
    ///     Used by the hands UI so the empty-hand icon is shown the first time a borg
    ///     module is opened, instead of only after closing and reopening it.
    /// </summary>
    public EntityUid EnsureDummy(Entity<HandPlaceholderVisualsComponent?, HandPlaceholderComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp1, ref ent.Comp2, logMissing: false))
            return EntityUid.Invalid;

        if (ent.Comp1.Dummy == EntityUid.Invalid && ent.Comp2.Prototype is { } proto)
            ent.Comp1.Dummy = Spawn(proto);

        return ent.Comp1.Dummy;
    }

    private void PlaceholderRemove(Entity<HandPlaceholderVisualsComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.Dummy != EntityUid.Invalid)
            QueueDel(ent.Comp.Dummy);
    }
}
