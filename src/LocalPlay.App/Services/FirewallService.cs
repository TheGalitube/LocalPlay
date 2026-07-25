using System.Diagnostics;

namespace LocalPlay.Services;

public static class FirewallService
{
    private const string LegacyRuleName = "LocalPlay (Private LAN)";
    private const string TcpRuleName = "LocalPlay (Private LAN TCP)";
    private const string UdpRuleName = "LocalPlay (Private LAN UDP)";

    public static void RequestPrivateLanRule(int portStart)
    {
        if (portStart is < 1024 or > 65533)
        {
            throw new ArgumentOutOfRangeException(
                nameof(portStart),
                "Der Startport muss zwischen 1024 und 65533 liegen.");
        }

        var enginePath = EngineLocator.Find()
            ?? throw new FileNotFoundException("Die AirPlay-Engine wurde nicht gefunden.");

        var escapedEnginePath = enginePath.Replace("'", "''");
        var portRange = $"{portStart}-{portStart + 2}";
        var command =
            $"Get-NetFirewallRule -DisplayName '{LegacyRuleName}','{TcpRuleName}','{UdpRuleName}' " +
            "-ErrorAction SilentlyContinue | Remove-NetFirewallRule; " +
            $"New-NetFirewallRule -DisplayName '{TcpRuleName}' -Direction Inbound -Action Allow " +
            $"-Program '{escapedEnginePath}' -Protocol TCP -LocalPort '{portRange}' " +
            "-Profile Private -RemoteAddress LocalSubnet; " +
            $"New-NetFirewallRule -DisplayName '{UdpRuleName}' -Direction Inbound -Action Allow " +
            $"-Program '{escapedEnginePath}' -Protocol UDP -LocalPort '5353','{portRange}' " +
            "-Profile Private -RemoteAddress LocalSubnet";

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            UseShellExecute = true,
            Verb = "runas"
        });
    }
}
