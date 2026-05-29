using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Containers;

namespace Content.Server.Storage.EntitySystems
{
    [UsedImplicitly]
    public sealed class ItemCounterSystem : SharedItemCounterSystem
    {
        [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
        protected override int? GetCount(ContainerModifiedMessage msg, ItemCounterComponent itemCounter)
        {
            if (!EntityManager.TryGetComponent(msg.Container.Owner, out StorageComponent? component))
            {
                return null;
            }

            var count = 0;
            foreach (var entity in component.Container.ContainedEntities)
            {
                if (_whitelistSystem.IsWhitelistPass(itemCounter.Count, entity))
                    count++;
            }

            return count;
        }

        // HardLight: Override to count items in container filtered by whitelist.
        protected override int? GetCurrentCount(EntityUid uid, ItemCounterComponent itemCounter)
        {
            if (!TryComp<StorageComponent>(uid, out var component))
                return null;

            var count = 0;
            foreach (var entity in component.Container.ContainedEntities)
            {
                if (_whitelistSystem.IsWhitelistPass(itemCounter.Count, entity))
                    count++;
            }

            return count;
        }
    }
}
