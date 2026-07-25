using System.Diagnostics;

namespace LocalPlay.Services;

public enum WindowsNetworkCategory
{
    Unknown = -1,
    Public = 0,
    Private = 1,
    DomainAuthenticated = 2
}

public static class WindowsNetworkProfileService
{
    public static WindowsNetworkCategory GetCategory(int interfaceIndex)
    {
        if (interfaceIndex <= 0)
        {
            return WindowsNetworkCategory.Unknown;
        }

        try
        {
            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(
                $"$profile = Get-NetConnectionProfile -InterfaceIndex {interfaceIndex} " +
                "-ErrorAction Stop | Select-Object -First 1; " +
                "Write-Output ([int]$profile.NetworkCategory)");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return WindowsNetworkCategory.Unknown;
            }

            if (!process.WaitForExit(5000))
            {
                process.Kill(true);
                return WindowsNetworkCategory.Unknown;
            }

            var output = process.StandardOutput.ReadToEnd();
            return process.ExitCode == 0 && int.TryParse(output.Trim(), out var category)
                && Enum.IsDefined(typeof(WindowsNetworkCategory), category)
                    ? (WindowsNetworkCategory)category
                    : WindowsNetworkCategory.Unknown;
        }
        catch
        {
            return WindowsNetworkCategory.Unknown;
        }
    }

    public static string Describe(WindowsNetworkCategory category) => category switch
    {
        WindowsNetworkCategory.Private => "Privates Windows-Netzwerk",
        WindowsNetworkCategory.Public => "Öffentliches Windows-Netzwerk",
        WindowsNetworkCategory.DomainAuthenticated => "Domänennetzwerk",
        _ => "Windows-Netzwerkprofil unbekannt"
    };
}
