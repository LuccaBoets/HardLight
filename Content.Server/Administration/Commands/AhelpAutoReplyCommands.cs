using System.Linq;
using Content.Server.Administration.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class AhelpAutoEnabledCommand : IConsoleCommand
{
    public string Command => "ahelpautoenabled";
    public string Description => "Gets or sets whether automated ahelp replies are enabled.";
    public string Help => "Usage: ahelpautoenabled [true|false]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var bwoink = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<BwoinkSystem>();

        if (args.Length == 0)
        {
            shell.WriteLine($"Automated ahelp replies: {(bwoink.AutoReplyEnabled ? "enabled" : "disabled")}");
            return;
        }

        if (!bool.TryParse(args[0], out var enabled))
        {
            shell.WriteError("Expected true or false.");
            return;
        }

        bwoink.SetAutoReplyEnabled(enabled);
        shell.WriteLine($"Automated ahelp replies {(enabled ? "enabled" : "disabled")}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AhelpAutoListCommand : IConsoleCommand
{
    public string Command => "ahelpautolist";
    public string Description => "Lists editable automated ahelp categories.";
    public string Help => "Usage: ahelpautolist";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var bwoink = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<BwoinkSystem>();
        var categories = bwoink.GetAutoReplyCategories().ToArray();

        if (categories.Length == 0)
        {
            shell.WriteLine("No automated ahelp categories are defined.");
            return;
        }

        shell.WriteLine("Automated ahelp categories:");
        foreach (var category in categories)
        {
            shell.WriteLine($"- {category}");
        }
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AhelpAutoGetCommand : IConsoleCommand
{
    public string Command => "ahelpautoget";
    public string Description => "Gets the current automated ahelp template for a category.";
    public string Help => "Usage: ahelpautoget <category>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError("Missing category.");
            return;
        }

        var category = args[0];
        var bwoink = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<BwoinkSystem>();

        if (!bwoink.TryGetAutoReplyTemplate(category, out var template))
        {
            shell.WriteError($"Unknown category: {category}");
            return;
        }

        shell.WriteLine(template);
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AhelpAutoSetCommand : IConsoleCommand
{
    public string Command => "ahelpautoset";
    public string Description => "Sets a live override template for an automated ahelp category.";
    public string Help => "Usage: ahelpautoset <category> <template>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError("Expected category and template.");
            return;
        }

        var category = args[0];
        var template = string.Join(" ", args.Skip(1));
        var bwoink = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<BwoinkSystem>();

        if (!bwoink.TrySetAutoReplyTemplate(category, template))
        {
            shell.WriteError($"Unknown category: {category}");
            return;
        }

        shell.WriteLine($"Updated automated ahelp template for '{category}'.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AhelpAutoResetCommand : IConsoleCommand
{
    public string Command => "ahelpautoreset";
    public string Description => "Resets an automated ahelp category to its localized default text.";
    public string Help => "Usage: ahelpautoreset <category>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError("Missing category.");
            return;
        }

        var category = args[0];
        var bwoink = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<BwoinkSystem>();

        if (!bwoink.TrySetAutoReplyTemplate(category, null))
        {
            shell.WriteError($"Unknown category: {category}");
            return;
        }

        shell.WriteLine($"Reset automated ahelp template for '{category}'.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AhelpTriageEnabledCommand : IConsoleCommand
{
    public string Command => "ahelptriageenabled";
    public string Description => "Gets or sets whether ahelp triage classification is enabled.";
    public string Help => "Usage: ahelptriageenabled [true|false]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var bwoink = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<BwoinkSystem>();

        if (args.Length == 0)
        {
            shell.WriteLine($"Ahelp triage: {(bwoink.TriageEnabled ? "enabled" : "disabled")}");
            return;
        }

        if (!bool.TryParse(args[0], out var enabled))
        {
            shell.WriteError("Expected true or false.");
            return;
        }

        bwoink.SetTriageEnabled(enabled);
        shell.WriteLine($"Ahelp triage {(enabled ? "enabled" : "disabled")}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AhelpTriageListCommand : IConsoleCommand
{
    public string Command => "ahelptriagelist";
    public string Description => "Lists editable triage categories.";
    public string Help => "Usage: ahelptriagelist";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var bwoink = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<BwoinkSystem>();
        var categories = bwoink.GetTriageCategories().ToArray();

        if (categories.Length == 0)
        {
            shell.WriteLine("No triage categories are defined.");
            return;
        }

        shell.WriteLine("Triage categories:");
        foreach (var category in categories)
        {
            shell.WriteLine($"- {category}");
        }
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AhelpTriageGetCommand : IConsoleCommand
{
    public string Command => "ahelptriageget";
    public string Description => "Gets active triage keywords for a category.";
    public string Help => "Usage: ahelptriageget <category>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError("Missing category.");
            return;
        }

        var category = args[0];
        var bwoink = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<BwoinkSystem>();

        if (!bwoink.TryGetTriageRuleKeywords(category, out var keywords))
        {
            shell.WriteError($"Unknown category: {category}");
            return;
        }

        shell.WriteLine(keywords);
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AhelpTriageSetCommand : IConsoleCommand
{
    public string Command => "ahelptriageset";
    public string Description => "Sets active triage keywords for a category using comma, semicolon, or newline separators.";
    public string Help => "Usage: ahelptriageset <category> <keywords>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError("Expected category and keywords.");
            return;
        }

        var category = args[0];
        var keywords = string.Join(" ", args.Skip(1));
        var bwoink = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<BwoinkSystem>();

        if (!bwoink.TrySetTriageRuleKeywords(category, keywords))
        {
            shell.WriteError($"Unknown category or invalid keywords: {category}");
            return;
        }

        shell.WriteLine($"Updated triage rule keywords for '{category}'.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AhelpTriageResetCommand : IConsoleCommand
{
    public string Command => "ahelptriagereset";
    public string Description => "Resets triage keywords for a category to defaults.";
    public string Help => "Usage: ahelptriagereset <category>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError("Missing category.");
            return;
        }

        var category = args[0];
        var bwoink = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<BwoinkSystem>();

        if (!bwoink.TrySetTriageRuleKeywords(category, null))
        {
            shell.WriteError($"Unknown category: {category}");
            return;
        }

        shell.WriteLine($"Reset triage rule keywords for '{category}'.");
    }
}