// VRS port: Goobstation/Starlight CollectiveMind — client-side glue that
// re-runs ChatManager.UpdatePermissions when the local player gains or
// loses a CollectiveMindComponent so the chat UI reflects channel access.

using Content.Client.Chat.Managers;
using Content.Shared._Starlight.CollectiveMind;
using Robust.Client.Player;

namespace Content.Client._Starlight.Chat;

public sealed class CollectiveMindSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CollectiveMindComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CollectiveMindComponent, ComponentRemove>(OnRemove);
    }

    public bool IsCollectiveMind => CompOrNull<CollectiveMindComponent>(_playerManager.LocalSession?.AttachedEntity) != null;

    private void OnInit(EntityUid uid, CollectiveMindComponent component, ComponentInit args)
    {
        _chatManager.UpdatePermissions();
    }

    private void OnRemove(EntityUid uid, CollectiveMindComponent component, ComponentRemove args)
    {
        _chatManager.UpdatePermissions();
    }
}
