using System.Diagnostics;
using System.Text.RegularExpressions;
using LocalPlay.Models;

namespace LocalPlay.Services;

public sealed partial class AirPlayEngine : IDisposable
{
    private Process? _process;
    private bool _requestedStop;

    public bool IsRunning => _process is { HasExited: false };
    public event Action<string>? LogReceived;
    public event Action<ReceiverState, string>? StateChanged;
    public event Action<string>? PinReceived;

    public Task StartAsync(AppSettings settings, string pairingRegisterPath)
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        var executable = EngineLocator.Find()
            ?? throw new FileNotFoundException(
                "Die AirPlay-Engine wurde nicht gefunden. Bitte zuerst scripts\\bootstrap.ps1 ausführen.");

        _requestedStop = false;
        ChangeState(ReceiverState.Starting, "Empfänger wird gestartet …");

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };

        var binDirectory = Path.GetDirectoryName(executable)!;
        startInfo.Environment["PATH"] =
            binDirectory + Path.PathSeparator + startInfo.Environment["PATH"];
        startInfo.Environment["GST_PLUGIN_SYSTEM_PATH_1_0"] =
            Path.Combine(binDirectory, "..", "lib", "gstreamer-1.0");

        AddArguments(startInfo, settings, pairingRegisterPath);

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += HandleOutput;
        _process.ErrorDataReceived += HandleOutput;
        _process.Exited += (_, _) =>
        {
            var exitCode = _process?.ExitCode ?? -1;
            ChangeState(
                _requestedStop ? ReceiverState.Stopped : ReceiverState.Faulted,
                _requestedStop ? "Empfänger ist aus" : $"Engine wurde beendet (Code {exitCode})");
        };

        if (!_process.Start())
        {
            ChangeState(ReceiverState.Faulted, "Engine konnte nicht gestartet werden");
            throw new InvalidOperationException("Die AirPlay-Engine konnte nicht gestartet werden.");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        return Task.CompletedTask;
    }

    private static void AddArguments(
        ProcessStartInfo startInfo,
        AppSettings settings,
        string pairingRegisterPath)
    {
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add(settings.ReceiverName);
        startInfo.ArgumentList.Add("-nh");
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add("7000,7001,7002");
        startInfo.ArgumentList.Add("-vs");
        startInfo.ArgumentList.Add("d3d11videosink");
        startInfo.ArgumentList.Add("-as");
        startInfo.ArgumentList.Add("wasapisink");
        startInfo.ArgumentList.Add("-vsync");
        startInfo.ArgumentList.Add("no");

        switch (settings.Quality)
        {
            case "1080p · 60 FPS":
                AddDisplayMode(startInfo, "1920x1080@60", 60, useHevc: false);
                break;
            case "2K · 30 FPS (HEVC)":
                AddDisplayMode(startInfo, "2560x1440@30", 30, useHevc: true);
                break;
            case "2K · 60 FPS (HEVC)":
                AddDisplayMode(startInfo, "2560x1440@60", 60, useHevc: true);
                break;
            case "4K · 30 FPS (HEVC)":
                AddDisplayMode(startInfo, "3840x2160@30", 30, useHevc: true);
                break;
            case "4K · 60 FPS (HEVC)":
                AddDisplayMode(startInfo, "3840x2160@60", 60, useHevc: true);
                break;
            default:
                AddDisplayMode(startInfo, "1920x1080@60", 30, useHevc: false);
                break;
        }

        if (settings.Fullscreen)
        {
            startInfo.ArgumentList.Add("-fs");
        }

        if (settings.RequirePin)
        {
            startInfo.ArgumentList.Add("-pin");
            startInfo.ArgumentList.Add("-reg");
            startInfo.ArgumentList.Add(pairingRegisterPath);
        }
    }

    private static void AddDisplayMode(
        ProcessStartInfo startInfo,
        string sizeAndRefreshRate,
        int maximumFramesPerSecond,
        bool useHevc)
    {
        if (useHevc)
        {
            startInfo.ArgumentList.Add("-h265");
        }

        startInfo.ArgumentList.Add("-s");
        startInfo.ArgumentList.Add(sizeAndRefreshRate);
        startInfo.ArgumentList.Add("-fps");
        startInfo.ArgumentList.Add(maximumFramesPerSecond.ToString());
    }

    private void HandleOutput(object sender, DataReceivedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(eventArgs.Data))
        {
            return;
        }

        var line = eventArgs.Data.Trim();
        LogReceived?.Invoke(line);

        var pinMatch = PinPattern().Match(line);
        if (pinMatch.Success)
        {
            PinReceived?.Invoke(pinMatch.Groups[1].Value);
            ChangeState(ReceiverState.Pairing, "PIN auf dem Apple-Gerät eingeben");
        }
        else if (line.Contains("connection request from", StringComparison.OrdinalIgnoreCase))
        {
            ChangeState(ReceiverState.Streaming, "Apple-Gerät verbunden");
        }
        else if (line.Contains("raop service", StringComparison.OrdinalIgnoreCase)
                 || line.Contains("server listening", StringComparison.OrdinalIgnoreCase)
                 || line.Contains("using system MAC", StringComparison.OrdinalIgnoreCase))
        {
            ChangeState(ReceiverState.Ready, "Bereit für AirPlay");
        }
        else if (line.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            LogReceived?.Invoke("Hinweis: Details im Diagnoseprotokoll prüfen.");
        }
    }

    public async Task StopAsync()
    {
        var process = _process;
        if (process is null || process.HasExited)
        {
            ChangeState(ReceiverState.Stopped, "Empfänger ist aus");
            return;
        }

        _requestedStop = true;
        try
        {
            process.Kill(true);
            await process.WaitForExitAsync();
        }
        catch (InvalidOperationException)
        {
            ChangeState(ReceiverState.Stopped, "Empfänger ist aus");
        }
    }

    private void ChangeState(ReceiverState state, string message) =>
        StateChanged?.Invoke(state, message);

    public void Dispose()
    {
        if (_process is { HasExited: false })
        {
            _requestedStop = true;
            _process.Kill(true);
        }

        _process?.Dispose();
    }

    [GeneratedRegex(@"PIN\s*=\s*[""']?(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex PinPattern();
}
