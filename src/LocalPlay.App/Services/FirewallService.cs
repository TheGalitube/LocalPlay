using System.Diagnostics;

namespace LocalPlay.Services;

public static class FirewallService
{
    private const string RuleName = "LocalPlay (Private LAN)";

    public static void RequestPrivateLanRule()
    {
        var enginePath = EngineLocator.Find()
            ?? throw new FileNotFoundException("Die AirPlay-Engine wurde nicht gefunden.");

        var escapedEnginePath = enginePath.Replace("'", "''");
        var command =
            $"Get-NetFirewallRule -DisplayName '{RuleName}' -ErrorAction SilentlyContinue | Remove-NetFirewallRule; " +
            $"New-NetFirewallRule -DisplayName '{RuleName}' -Direction Inbound -Action Allow " +
            $"-Program '{escapedEnginePath}' -Profile Private -RemoteAddress LocalSubnet";

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            UseShellExecute = true,
            Verb = "runas"
        });
    }
}

