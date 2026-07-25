using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LocalPlay.Services;

public static class NetworkInfoService
{
    public static string GetLocalAddress()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .Where(network => network.NetworkInterfaceType is NetworkInterfaceType.Wireless80211
                or NetworkInterfaceType.Ethernet)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
            .Where(address => !IPAddress.IsLoopback(address))
            .ToArray();

        return candidates.FirstOrDefault()?.ToString() ?? "Keine LAN-Adresse";
    }
}

