using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Robust.Shared.Console;
using Robust.Shared.Enums;

namespace Content.Server._Floof.Chat.Commands
{
    [AnyCommand]
    internal sealed class SubtleOOCCommand : IConsoleCommand
    {
        public string Command => "sooc";
        public string Description => "Send a subtle OOC message visible to nearby players.";
        public string Help => "sooc <text>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (shell.Player is not { } player)
            {
                shell.WriteError("This command cannot be run from the server.");
                return;
            }

            if (player.Status != SessionStatus.InGame)
                return;

            if (player.AttachedEntity is not {} playerEntity)
            {
                shell.WriteError("You don't have an entity!");
                return;
            }

            if (args.Length < 1)
                return;

            var message = string.Join(" ", args).Trim();
            if (string.IsNullOrEmpty(message))
                return;

            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ChatSystem>()
                .TrySendInGameOOCMessage(playerEntity, message, InGameOOCChatType.SubtleLOOC, false, shell, player);
        }
    }
}
