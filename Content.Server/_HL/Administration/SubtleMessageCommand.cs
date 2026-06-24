using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.HL.Administration;

[AdminCommand(AdminFlags.Admin)]
public sealed class SubtleMessageCommand : IConsoleCommand
{
    public string Command => "subtlemessageui";
    public string Description => "Opens the subtle message UI.";
    public string Help => Command;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        var eui = IoCManager.Resolve<EuiManager>();
        eui.OpenEui(new SubtleMessageEui(null, null), player);
    }
}
