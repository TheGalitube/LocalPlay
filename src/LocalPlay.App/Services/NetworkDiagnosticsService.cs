using System.Net;
using System.Net.Sockets;
using LocalPlay.Models;

namespace LocalPlay.Services;

public sealed record NetworkDiagnosticResult(bool IsSuccessful, string Title, string Details);

public static class NetworkDiagnosticsService
{
    public static NetworkDiagnosticResult Run(
        AppSettings settings,
        IEnumerable<NetworkAdapterOption> adapters,
        bool engineIsRunning)
    {
        var adapter = NetworkInfoService.ResolveAdapter(adapters, settings.NetworkAdapterId);
        if (adapter is null)
        {
            return new NetworkDiagnosticResult(
                false,
                "Kein aktiver Netzwerkadapter",
                "Verbinde den PC mit Ethernet oder WLAN und aktualisiere die Adapterliste.");
        }

        if (EngineLocator.Find() is null)
        {
            return new NetworkDiagnosticResult(
                false,
                "AirPlay-Engine fehlt",
                "Führe scripts\\bootstrap.ps1 aus, damit UxPlay installiert wird.");
        }

        if (settings.PortStart is < 1024 or > 65533)
        {
            return new NetworkDiagnosticResult(
                false,
                "Portbereich ist ungültig",
                "Der Startport muss zwischen 1024 und 65533 liegen.");
        }

        var networkCategory =
            WindowsNetworkProfileService.GetCategory(adapter.InterfaceIndex);
        if (networkCategory == WindowsNetworkCategory.Public
            && !settings.AllowPublicNetworks)
        {
            return new NetworkDiagnosticResult(
                false,
                "Öffentliches Netzwerk ist blockiert",
                "Windows stuft diese Verbindung als öffentlich ein. Aktiviere die Option " +
                "für öffentliche Netzwerke oder ändere das Windows-Netzwerkprofil.");
        }

        if (networkCategory == WindowsNetworkCategory.Unknown)
        {
            return new NetworkDiagnosticResult(
                false,
                "Netzwerkprofil nicht erkannt",
                "Wähle den echten Ethernet- oder WLAN-Adapter statt eines virtuellen Adapters.");
        }

        var firewallStatus = FirewallService.GetRuleStatus(
            settings.PortStart,
            settings.AllowPublicNetworks);
        if (firewallStatus != FirewallRuleStatus.Ready)
        {
            var details = firewallStatus switch
            {
                FirewallRuleStatus.Missing =>
                    "Die LocalPlay-Firewall-Regeln fehlen auf diesem PC.",
                FirewallRuleStatus.Mismatch =>
                    "Die vorhandenen Regeln passen nicht zu Engine, Ports oder Netzwerkprofil.",
                _ =>
                    "Der Status der Windows-Firewall konnte nicht verifiziert werden."
            };
            return new NetworkDiagnosticResult(
                false,
                "Firewall-Freigabe erforderlich",
                $"{details} Klicke auf „Firewall-Regeln einrichten“.");
        }

        var profileDescription =
            WindowsNetworkProfileService.Describe(networkCategory);
        if (engineIsRunning)
        {
            return new NetworkDiagnosticResult(
                true,
                "Empfänger ist im LAN aktiv",
                $"{adapter.Name} · {adapter.IPv4Address} · {profileDescription} · " +
                $"Ports {settings.PortStart}–{settings.PortStart + 2}");
        }

        var unavailablePorts = Enumerable.Range(settings.PortStart, 3)
            .Where(port => !CanBind(port))
            .ToArray();

        if (unavailablePorts.Length > 0)
        {
            return new NetworkDiagnosticResult(
                false,
                "Mindestens ein Port ist belegt",
                $"Nicht verfügbar: {string.Join(", ", unavailablePorts)}. Wähle einen anderen Startport.");
        }

        return new NetworkDiagnosticResult(
            true,
            "Netzwerk ist bereit",
            $"{adapter.Name} · {adapter.IPv4Address} · {profileDescription} · " +
            $"Ports {settings.PortStart}–{settings.PortStart + 2} sind frei.");
    }

    private static bool CanBind(int port)
    {
        Socket? tcpSocket = null;
        Socket? udpSocket = null;
        try
        {
            tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ExclusiveAddressUse = true
            };
            tcpSocket.Bind(new IPEndPoint(IPAddress.Any, port));

            udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                ExclusiveAddressUse = true
            };
            udpSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            tcpSocket?.Dispose();
            udpSocket?.Dispose();
        }
    }
}
