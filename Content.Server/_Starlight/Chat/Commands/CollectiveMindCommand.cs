// VRS port: Goobstation/Starlight CollectiveMind — `cmsay` console command.
// Sends an in-character chat message routed through ChatSystem with the
// CollectiveMind chat type. The `+` channel-key prefix is required, e.g.
// `cmsay +b hello world` -> Binary collective mind.

using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server.Chat.Commands;

[AnyCommand]
internal sealed class CollectiveMindCommand : IConsoleCommand
{
    public string Command => "cmsay";
    public string Description => "Send chat messages to the collective mind. Requires a `+<key>` channel prefix.";
    public string Help => "cmsay +<key> <text>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError("This command cannot be run from the server.");
            return;
        }

        if (player.Status != SessionStatus.InGame)
            return;

        if (player.AttachedEntity is not { } playerEntity)
        {
            shell.WriteError("You don't have an entity!");
            return;
        }

        if (args.Length < 1)
            return;

        var message = string.Join(" ", args).Trim();
        if (string.IsNullOrEmpty(message))
            return;

        EntitySystem.Get<ChatSystem>().TrySendInGameICMessage(playerEntity, message, InGameICChatType.CollectiveMind, ChatTransmitRange.Normal);
    }
}
