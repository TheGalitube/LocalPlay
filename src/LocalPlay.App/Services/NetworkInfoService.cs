using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LocalPlay.Models;

namespace LocalPlay.Services;

public static class NetworkInfoService
{
    public static IReadOnlyList<NetworkAdapterOption> GetAdapters()
    {
        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .Where(network => network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(network =>
            {
                var properties = network.GetIPProperties();
                var address = properties.UnicastAddresses
                    .Select(item => item.Address)
                    .FirstOrDefault(IsUsableIPv4);
                var hasGateway = properties.GatewayAddresses
                    .Select(item => item.Address)
                    .Any(item => item.AddressFamily == AddressFamily.InterNetwork
                        && !item.Equals(IPAddress.Any));

                return address is null
                    ? null
                    : new NetworkAdapterOption(
                        network.Id,
                        network.Name,
                        address.ToString(),
                        properties.GetIPv4Properties()?.Index ?? 0,
                        hasGateway,
                        DescribeKind(network.NetworkInterfaceType));
            })
            .Where(option => option is not null)
            .Cast<NetworkAdapterOption>()
            .OrderByDescending(option => option.HasGateway)
            .ThenByDescending(option => option.Kind is "Ethernet" or "WLAN")
            .ThenBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return
        [
            new NetworkAdapterOption(
                string.Empty,
                "Automatisch",
                string.Empty,
                0,
                false,
                string.Empty,
                IsAutomatic: true),
            .. adapters
        ];
    }

    public static NetworkAdapterOption? ResolveAdapter(
        IEnumerable<NetworkAdapterOption> adapters,
        string? selectedId)
    {
        var available = adapters.Where(option => !option.IsAutomatic).ToArray();
        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            var selected = available.FirstOrDefault(
                option => string.Equals(option.Id, selectedId, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return selected;
            }
        }

        return available.FirstOrDefault(option => option.HasGateway)
            ?? available.FirstOrDefault();
    }

    public static string GetLocalAddress(string? selectedId = null)
    {
        var adapters = GetAdapters();
        return ResolveAdapter(adapters, selectedId)?.IPv4Address ?? "Keine LAN-Adresse";
    }

    private static bool IsUsableIPv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork
            || IPAddress.IsLoopback(address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return !(bytes[0] == 169 && bytes[1] == 254);
    }

    private static string DescribeKind(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Wireless80211 => "WLAN",
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
            or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.FastEthernetT => "Ethernet",
        NetworkInterfaceType.Tunnel => "Tunnel/VPN",
        _ => "Netzwerkadapter"
    };
}
