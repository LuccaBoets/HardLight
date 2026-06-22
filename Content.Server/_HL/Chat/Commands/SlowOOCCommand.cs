using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._HL.Chat.Commands;

/// <summary>
///     Admin command to enable/disable OOC slow mode and configure the interval.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class SlowOOCCommand : IConsoleCommand
{
    public string Command => "slowooc";
    public string Description => Loc.GetString("slow-ooc-command-description");
    public string Help => Loc.GetString("slow-ooc-command-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();

        if (args.Length > 2)
        {
            shell.WriteError(Loc.GetString("slow-ooc-command-too-many-arguments-error"));
            return;
        }

        var enabled = cfg.GetCVar(CCVars.OocSlowModeEnabled);

        if (args.Length == 0)
        {
            // Toggle
            enabled = !enabled;
        }
        else
        {
            if (!bool.TryParse(args[0], out enabled))
            {
                shell.WriteError(Loc.GetString("slow-ooc-command-invalid-argument-error"));
                return;
            }

            if (args.Length == 2)
            {
                if (!float.TryParse(args[1], out var interval) || interval <= 0f)
                {
                    shell.WriteError(Loc.GetString("slow-ooc-command-invalid-interval-error"));
                    return;
                }

                cfg.SetCVar(CCVars.OocSlowModeInterval, interval);
                shell.WriteLine(Loc.GetString("slow-ooc-command-interval-set", ("seconds", (int)interval)));
            }
        }

        cfg.SetCVar(CCVars.OocSlowModeEnabled, enabled);

        var currentInterval = (int)cfg.GetCVar(CCVars.OocSlowModeInterval);
        shell.WriteLine(Loc.GetString(
            enabled ? "slow-ooc-command-enabled" : "slow-ooc-command-disabled",
            ("seconds", currentInterval)));
    }
}
