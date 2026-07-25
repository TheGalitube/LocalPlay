using System.ComponentModel;
using System.Diagnostics;

namespace LocalPlay.Services;

public enum FirewallRuleStatus
{
    Ready,
    Missing,
    Mismatch,
    Unavailable
}

public sealed record FirewallConfigurationResult(
    bool IsSuccessful,
    bool WasCanceled,
    string Message);

public static class FirewallService
{
    private const string LegacyRuleName = "LocalPlay (Private LAN)";
    private const string LegacyTcpRuleName = "LocalPlay (Private LAN TCP)";
    private const string LegacyUdpRuleName = "LocalPlay (Private LAN UDP)";
    private const string TcpRuleName = "LocalPlay-LAN-TCP";
    private const string UdpRuleName = "LocalPlay-LAN-UDP";
    private const string TcpDisplayName = "LocalPlay (LAN TCP)";
    private const string UdpDisplayName = "LocalPlay (LAN UDP + mDNS)";

    public static FirewallRuleStatus GetRuleStatus(int portStart, bool allowPublicNetworks)
    {
        ValidatePort(portStart);

        var enginePath = EngineLocator.Find();
        if (enginePath is null)
        {
            return FirewallRuleStatus.Unavailable;
        }

        var escapedEnginePath = EscapePowerShellLiteral(enginePath);
        var portRange = $"{portStart}-{portStart + 2}";
        var expectedProfiles = allowPublicNetworks ? 7 : 3;
        var command =
            "$ErrorActionPreference = 'Stop'; " +
            $"$tcp = Get-NetFirewallRule -Name '{TcpRuleName}' -ErrorAction SilentlyContinue; " +
            $"$udp = Get-NetFirewallRule -Name '{UdpRuleName}' -ErrorAction SilentlyContinue; " +
            "if (-not $tcp -or -not $udp) { exit 10 }; " +
            "$tcpApp = $tcp | Get-NetFirewallApplicationFilter; " +
            "$udpApp = $udp | Get-NetFirewallApplicationFilter; " +
            "$tcpPort = $tcp | Get-NetFirewallPortFilter; " +
            "$udpPort = $udp | Get-NetFirewallPortFilter; " +
            "$tcpAddress = $tcp | Get-NetFirewallAddressFilter; " +
            "$udpAddress = $udp | Get-NetFirewallAddressFilter; " +
            $"$tcpProfileValid = [int]$tcp.Profile -eq {expectedProfiles} " +
            (allowPublicNetworks ? "-or [int]$tcp.Profile -eq 0; " : "; ") +
            $"$udpProfileValid = [int]$udp.Profile -eq {expectedProfiles} " +
            (allowPublicNetworks ? "-or [int]$udp.Profile -eq 0; " : "; ") +
            $"$valid = $tcp.Enabled -eq 'True' -and $udp.Enabled -eq 'True' " +
            "-and $tcpProfileValid -and $udpProfileValid " +
            $"-and $tcpApp.Program -ieq '{escapedEnginePath}' " +
            $"-and $udpApp.Program -ieq '{escapedEnginePath}' " +
            "-and $tcpPort.Protocol -eq 'TCP' -and $udpPort.Protocol -eq 'UDP' " +
            $"-and @($tcpPort.LocalPort) -contains '{portRange}' " +
            $"-and @($udpPort.LocalPort) -contains '{portRange}' " +
            "-and @($udpPort.LocalPort) -contains '5353' " +
            "-and @($tcpAddress.RemoteAddress) -contains 'LocalSubnet' " +
            "-and @($udpAddress.RemoteAddress) -contains 'LocalSubnet'; " +
            "if ($valid) { exit 0 } else { exit 11 }";

        return RunPowerShell(command) switch
        {
            0 => FirewallRuleStatus.Ready,
            10 => FirewallRuleStatus.Missing,
            11 => FirewallRuleStatus.Mismatch,
            _ => FirewallRuleStatus.Unavailable
        };
    }

    public static async Task<FirewallConfigurationResult> ConfigureLocalNetworkRulesAsync(
        int portStart,
        bool allowPublicNetworks)
    {
        ValidatePort(portStart);

        var enginePath = EngineLocator.Find()
            ?? throw new FileNotFoundException("Die AirPlay-Engine wurde nicht gefunden.");

        var escapedEnginePath = EscapePowerShellLiteral(enginePath);
        var portRange = $"{portStart}-{portStart + 2}";
        var profiles = allowPublicNetworks
            ? "Domain,Private,Public"
            : "Domain,Private";
        var command =
            "$ErrorActionPreference = 'Stop'; " +
            $"Get-NetFirewallRule -Name '{TcpRuleName}','{UdpRuleName}' " +
            "-ErrorAction SilentlyContinue | Remove-NetFirewallRule; " +
            $"Get-NetFirewallRule -DisplayName '{LegacyRuleName}','{LegacyTcpRuleName}'," +
            $"'{LegacyUdpRuleName}' -ErrorAction SilentlyContinue | Remove-NetFirewallRule; " +
            $"New-NetFirewallRule -Name '{TcpRuleName}' -DisplayName '{TcpDisplayName}' " +
            "-Group 'LocalPlay' -Direction Inbound -Action Allow " +
            $"-Program '{escapedEnginePath}' -Protocol TCP -LocalPort '{portRange}' " +
            $"-Profile {profiles} -RemoteAddress LocalSubnet -EdgeTraversalPolicy Block; " +
            $"New-NetFirewallRule -Name '{UdpRuleName}' -DisplayName '{UdpDisplayName}' " +
            "-Group 'LocalPlay' -Direction Inbound -Action Allow " +
            $"-Program '{escapedEnginePath}' -Protocol UDP -LocalPort '5353','{portRange}' " +
            $"-Profile {profiles} -RemoteAddress LocalSubnet -EdgeTraversalPolicy Block";

        try
        {
            var startInfo = CreatePowerShellStartInfo(command, elevate: true);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new FirewallConfigurationResult(
                    false,
                    false,
                    "Die Windows-Firewall konnte nicht geöffnet werden.");
            }

            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                return new FirewallConfigurationResult(
                    false,
                    false,
                    $"Windows konnte die Firewall-Regeln nicht anlegen (Code {process.ExitCode}).");
            }

            var status = GetRuleStatus(portStart, allowPublicNetworks);
            return status == FirewallRuleStatus.Ready
                ? new FirewallConfigurationResult(
                    true,
                    false,
                    "Die LocalPlay-Firewall-Regeln sind aktiv.")
                : new FirewallConfigurationResult(
                    false,
                    false,
                    "Die Firewall-Regeln wurden nicht vollständig übernommen. " +
                    "Eine Sicherheitsrichtlinie oder Drittanbieter-Firewall kann sie blockieren.");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return new FirewallConfigurationResult(
                false,
                true,
                "Die Administratorabfrage wurde abgebrochen.");
        }
    }

    private static int RunPowerShell(string command)
    {
        try
        {
            var startInfo = CreatePowerShellStartInfo(command, elevate: false);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return -1;
            }

            if (!process.WaitForExit(8000))
            {
                process.Kill(true);
                return -1;
            }

            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static ProcessStartInfo CreatePowerShellStartInfo(string command, bool elevate)
    {
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = elevate,
            CreateNoWindow = !elevate
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(command);
        if (elevate)
        {
            startInfo.Verb = "runas";
        }

        return startInfo;
    }

    private static string EscapePowerShellLiteral(string value) =>
        value.Replace("'", "''");

    private static void ValidatePort(int portStart)
    {
        if (portStart is < 1024 or > 65533)
        {
            throw new ArgumentOutOfRangeException(
                nameof(portStart),
                "Der Startport muss zwischen 1024 und 65533 liegen.");
        }
    }
}
