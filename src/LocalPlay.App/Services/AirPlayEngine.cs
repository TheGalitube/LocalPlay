using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using LocalPlay.Models;

namespace LocalPlay.Services;

public sealed partial class AirPlayEngine : IDisposable
{
    private static readonly TimeSpan NetworkChangeDebounce = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RecoveryRetryDelay = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly object _stateLock = new();
    private Process? _process;
    private AppSettings? _settings;
    private string? _pairingRegisterPath;
    private CancellationTokenSource? _recoveryCancellation;
    private bool _networkEventsSubscribed;
    private bool _shouldRun;
    private bool _disposed;

    public bool IsRunning
    {
        get
        {
            lock (_stateLock)
            {
                try
                {
                    return _process is { HasExited: false };
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }
    }

    public event Action<string>? LogReceived;
    public event Action<ReceiverState, string>? StateChanged;
    public event Action<string>? PinReceived;
    public event Action? ClientConnected;

    public async Task StartAsync(AppSettings settings, string pairingRegisterPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _shouldRun = true;
        _settings = CopySettings(settings);
        _pairingRegisterPath = pairingRegisterPath;
        SubscribeToNetworkChanges();
        CancelRecovery();

        await _lifecycleLock.WaitAsync();
        try
        {
            if (IsRunning)
            {
                return;
            }

            await StartProcessAsync(_settings, pairingRegisterPath);
        }
        catch
        {
            _shouldRun = false;
            UnsubscribeFromNetworkChanges();
            throw;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task StartProcessAsync(AppSettings settings, string pairingRegisterPath)
    {
        var executable = EngineLocator.Find()
            ?? throw new FileNotFoundException(
                "Die AirPlay-Engine wurde nicht gefunden. Bitte zuerst scripts\\bootstrap.ps1 ausführen.");
        var selectedAdapter = NetworkInfoService.ResolveAdapter(
            NetworkInfoService.GetAdapters(),
            settings.NetworkAdapterId)
            ?? throw new NetworkUnavailableException();

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
        var bundledPluginDirectory = Path.Combine(binDirectory, "lib", "gstreamer-1.0");
        var systemPluginDirectory =
            Path.GetFullPath(Path.Combine(binDirectory, "..", "lib", "gstreamer-1.0"));
        startInfo.Environment["GST_PLUGIN_SYSTEM_PATH_1_0"] =
            Directory.Exists(bundledPluginDirectory)
                ? bundledPluginDirectory
                : systemPluginDirectory;

        var bundledPluginScanner = Path.Combine(
            binDirectory,
            "libexec",
            "gstreamer-1.0",
            "gst-plugin-scanner.exe");
        var systemPluginScanner = Path.GetFullPath(Path.Combine(
            binDirectory,
            "..",
            "libexec",
            "gstreamer-1.0",
            "gst-plugin-scanner.exe"));
        var pluginScanner = File.Exists(bundledPluginScanner)
            ? bundledPluginScanner
            : systemPluginScanner;
        if (File.Exists(pluginScanner))
        {
            startInfo.Environment["GST_PLUGIN_SCANNER"] = pluginScanner;
        }

        var registryDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LocalPlay");
        Directory.CreateDirectory(registryDirectory);
        var engineTimestamp = File.GetLastWriteTimeUtc(executable).Ticks.ToString("x");
        startInfo.Environment["GST_REGISTRY"] =
            Path.Combine(registryDirectory, $"gstreamer-registry-{engineTimestamp}.bin");
        startInfo.Environment["UXPLAY_MDNS_IPV4"] = selectedAdapter.IPv4Address;

        var videoPipeline = await ResolveVideoPipelineAsync(startInfo, settings);
        AddArguments(startInfo, settings, pairingRegisterPath, videoPipeline);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += HandleOutput;
        process.ErrorDataReceived += HandleOutput;
        process.Exited += (_, _) => HandleUnexpectedExit(process);

        lock (_stateLock)
        {
            _process = process;
        }

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Die AirPlay-Engine konnte nicht gestartet werden.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            LogReceived?.Invoke(
                $"mDNS wird auf {selectedAdapter.Name} ({selectedAdapter.IPv4Address}) beworben.");
            LogReceived?.Invoke(videoPipeline.StatusMessage);
            LogReceived?.Invoke(string.Equals(
                    settings.PlaybackProfile,
                    "SynchronizedVideo",
                    StringComparison.Ordinal)
                ? "Wiedergabeprofil: Video · A/V-synchron."
                : "Wiedergabeprofil: Videoschnitt · geringe Latenz; Frames werden ohne zusätzliche Zeitstempel-Pufferung ausgegeben.");
            await Task.CompletedTask;
        }
        catch
        {
            lock (_stateLock)
            {
                if (ReferenceEquals(_process, process))
                {
                    _process = null;
                }
            }

            process.Dispose();
            ChangeState(ReceiverState.Faulted, "Engine konnte nicht gestartet werden");
            throw;
        }
    }

    private static void AddArguments(
        ProcessStartInfo startInfo,
        AppSettings settings,
        string pairingRegisterPath,
        VideoPipelineConfiguration videoPipeline)
    {
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add(settings.ReceiverName);
        startInfo.ArgumentList.Add("-nh");
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add(
            $"{settings.PortStart},{settings.PortStart + 1},{settings.PortStart + 2}");
        startInfo.ArgumentList.Add("-vs");
        startInfo.ArgumentList.Add("d3d11videosink");
        startInfo.ArgumentList.Add("-as");
        startInfo.ArgumentList.Add("wasapisink");
        startInfo.ArgumentList.Add("-vsync");
        if (!string.Equals(
                settings.PlaybackProfile,
                "SynchronizedVideo",
                StringComparison.Ordinal))
        {
            startInfo.ArgumentList.Add("no");
        }

        if (videoPipeline.UseD3D11HardwareDecoding)
        {
            startInfo.ArgumentList.Add("-vd");
            startInfo.ArgumentList.Add("d3d11h264dec");
            startInfo.ArgumentList.Add("-vc");
            startInfo.ArgumentList.Add("d3d11convert");
        }

        // Let a reconnect replace a stale session and reset dead clients sooner than
        // UxPlay's 15-second default without reacting to brief WLAN jitter.
        startInfo.ArgumentList.Add("-nohold");
        startInfo.ArgumentList.Add("-reset");
        startInfo.ArgumentList.Add("8");

        switch (videoPipeline.EffectiveQuality)
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

    private async Task<VideoPipelineConfiguration> ResolveVideoPipelineAsync(
        ProcessStartInfo engineStartInfo,
        AppSettings settings)
    {
        var requestedQuality = settings.Quality;
        var requiresHevc = requestedQuality.Contains("(HEVC)", StringComparison.Ordinal);
        var inspector = Path.Combine(
            Path.GetDirectoryName(engineStartInfo.FileName)!,
            "gst-inspect-1.0.exe");

        if (!File.Exists(inspector))
        {
            return new VideoPipelineConfiguration(
                requestedQuality,
                false,
                "GStreamer-Hardwareprüfung nicht verfügbar; Zielauflösung bleibt erhalten und die kompatible Decoderwahl ist aktiv.");
        }

        try
        {
            var inspectInfo = new ProcessStartInfo(inspector)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = engineStartInfo.WorkingDirectory
            };
            foreach (var variable in engineStartInfo.Environment)
            {
                inspectInfo.Environment[variable.Key] = variable.Value;
            }

            inspectInfo.ArgumentList.Add("d3d11");
            using var inspectorProcess = Process.Start(inspectInfo)
                ?? throw new InvalidOperationException("gst-inspect konnte nicht gestartet werden.");
            var standardOutput = inspectorProcess.StandardOutput.ReadToEndAsync();
            var standardError = inspectorProcess.StandardError.ReadToEndAsync();
            // On the first run gst-inspect also creates the shared plugin registry.
            // UxPlay would pay the same cost immediately afterwards, so allow that
            // bounded initialization to finish and reuse its registry.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            try
            {
                await inspectorProcess.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                inspectorProcess.Kill(entireProcessTree: true);
                throw new TimeoutException("GStreamer-Hardwareprüfung hat nicht rechtzeitig geantwortet.");
            }

            var output = (await standardOutput) + Environment.NewLine + (await standardError);
            var hasConverter = HasGStreamerFeature(output, "d3d11convert");
            var hasH264Decoder = HasGStreamerFeature(output, "d3d11h264dec");
            var hasH265Decoder = HasGStreamerFeature(output, "d3d11h265dec");
            var supportsRequestedCodec = hasH264Decoder && (!requiresHevc || hasH265Decoder);

            if (inspectorProcess.ExitCode == 0 && hasConverter && supportsRequestedCodec)
            {
                return new VideoPipelineConfiguration(
                    requestedQuality,
                    true,
                    requiresHevc
                        ? "Direct3D11-Hardwaredecoding für H.264/HEVC aktiv; 2K/4K bleibt auf der GPU."
                        : "Direct3D11-Hardwaredecoding für H.264 aktiv; Video bleibt auf der GPU.");
            }

            if (requiresHevc)
            {
                return new VideoPipelineConfiguration(
                    "1080p · 60 FPS",
                    hasConverter && hasH264Decoder,
                    "Kein vollständiger Direct3D11-HEVC-Pfad erkannt; automatischer Fallback auf 1080p · 60 FPS.");
            }
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            and not StackOverflowException)
        {
            LogReceived?.Invoke($"Hardwareprüfung übersprungen: {exception.Message}");
            return new VideoPipelineConfiguration(
                requestedQuality,
                false,
                "Hardwarefähigkeit war nicht sicher prüfbar; Zielauflösung bleibt erhalten und GStreamer wählt den Decoder automatisch.");
        }

        return new VideoPipelineConfiguration(
            requestedQuality,
            false,
            "Direct3D11-Hardwaredecoder nicht verfügbar; GStreamer wählt einen kompatiblen Decoder automatisch.");
    }

    private static bool HasGStreamerFeature(string inspectionOutput, string feature) =>
        inspectionOutput.Contains(
            $"  {feature}:",
            StringComparison.OrdinalIgnoreCase);

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
            ClientConnected?.Invoke();
            ChangeState(ReceiverState.Streaming, "Apple-Gerät verbunden");
        }
        else if (line.Contains("raop service", StringComparison.OrdinalIgnoreCase)
                 || line.Contains("server listening", StringComparison.OrdinalIgnoreCase)
                 || line.Contains("using system MAC", StringComparison.OrdinalIgnoreCase))
        {
            ChangeState(ReceiverState.Ready, "Bereit für AirPlay");
        }
        else if (IsMdnsFailure(line))
        {
            LogReceived?.Invoke("mDNS-Fehler erkannt; die Netzwerkdienste werden erneuert.");
            ScheduleRecovery("mDNS-Ankündigung fehlgeschlagen", TimeSpan.FromSeconds(1));
        }
        else if (line.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            LogReceived?.Invoke("Hinweis: Details im Diagnoseprotokoll prüfen.");
        }
    }

    public async Task StopAsync()
    {
        _shouldRun = false;
        CancelRecovery();
        UnsubscribeFromNetworkChanges();

        await _lifecycleLock.WaitAsync();
        try
        {
            await StopProcessAsync();
            ChangeState(ReceiverState.Stopped, "Empfänger ist aus");
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task StopProcessAsync()
    {
        Process? process;
        lock (_stateLock)
        {
            process = _process;
            _process = null;
        }

        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and termination.
        }
        finally
        {
            process.Dispose();
        }
    }

    private void SubscribeToNetworkChanges()
    {
        if (_networkEventsSubscribed)
        {
            return;
        }

        NetworkChange.NetworkAddressChanged += HandleNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged += HandleNetworkAvailabilityChanged;
        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkChanges()
    {
        if (!_networkEventsSubscribed)
        {
            return;
        }

        NetworkChange.NetworkAddressChanged -= HandleNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged -= HandleNetworkAvailabilityChanged;
        _networkEventsSubscribed = false;
    }

    private void HandleNetworkAddressChanged(object? sender, EventArgs eventArgs) =>
        ScheduleRecovery("Netzwerkadresse wurde geändert", NetworkChangeDebounce);

    private void HandleNetworkAvailabilityChanged(
        object? sender,
        NetworkAvailabilityEventArgs eventArgs) =>
        ScheduleRecovery("Netzwerkverfügbarkeit wurde geändert", NetworkChangeDebounce);

    private void ScheduleRecovery(string reason, TimeSpan delay)
    {
        if (!_shouldRun || _disposed)
        {
            return;
        }

        CancellationTokenSource cancellation;
        lock (_stateLock)
        {
            _recoveryCancellation?.Cancel();
            cancellation = new CancellationTokenSource();
            _recoveryCancellation = cancellation;
        }

        _ = RecoverAsync(reason, delay, cancellation);
    }

    private async Task RecoverAsync(
        string reason,
        TimeSpan delay,
        CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(delay, cancellation.Token);
            var firstAttempt = true;

            while (_shouldRun)
            {
                await _lifecycleLock.WaitAsync(cancellation.Token);
                try
                {
                    if (!_shouldRun || _settings is null || _pairingRegisterPath is null)
                    {
                        return;
                    }

                    if (firstAttempt)
                    {
                        LogReceived?.Invoke($"{reason}; AirPlay-Dienste werden neu gestartet.");
                        ChangeState(ReceiverState.Starting, "Netzwerkdienste werden erneuert …");
                        await StopProcessAsync();
                        firstAttempt = false;
                    }

                    try
                    {
                        await StartProcessAsync(_settings, _pairingRegisterPath);
                        LogReceived?.Invoke("AirPlay-Dienste wurden erfolgreich neu beworben.");
                        return;
                    }
                    catch (NetworkUnavailableException)
                    {
                        ChangeState(
                            ReceiverState.Starting,
                            "Warte auf eine aktive LAN-Verbindung …");
                        LogReceived?.Invoke(
                            "Noch keine aktive IPv4-LAN-Verbindung; neuer Versuch in 5 Sekunden.");
                    }
                    catch (Exception exception)
                    {
                        ChangeState(ReceiverState.Faulted, "Wiederherstellung wird erneut versucht …");
                        LogReceived?.Invoke(
                            $"Neustart fehlgeschlagen: {exception.Message} Neuer Versuch in 5 Sekunden.");
                    }
                }
                finally
                {
                    _lifecycleLock.Release();
                }

                await Task.Delay(RecoveryRetryDelay, cancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer network event or an explicit stop superseded this recovery.
        }
        finally
        {
            lock (_stateLock)
            {
                if (ReferenceEquals(_recoveryCancellation, cancellation))
                {
                    _recoveryCancellation = null;
                }
            }

            cancellation.Dispose();
        }
    }

    private void HandleUnexpectedExit(Process process)
    {
        bool recover;
        int exitCode;
        try
        {
            exitCode = process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            exitCode = -1;
        }

        lock (_stateLock)
        {
            recover = ReferenceEquals(_process, process) && _shouldRun;
            if (ReferenceEquals(_process, process))
            {
                _process = null;
            }
        }

        if (!recover)
        {
            return;
        }

        process.Dispose();
        ChangeState(ReceiverState.Faulted, $"Engine wurde beendet (Code {exitCode})");
        LogReceived?.Invoke("Die Engine wurde unerwartet beendet; automatischer Neustart folgt.");
        ScheduleRecovery("Engine wurde unerwartet beendet", TimeSpan.FromSeconds(2));
    }

    private void CancelRecovery()
    {
        lock (_stateLock)
        {
            _recoveryCancellation?.Cancel();
        }
    }

    private static bool IsMdnsFailure(string line) =>
        line.Contains("mDNS IPv4 send error", StringComparison.OrdinalIgnoreCase)
        || line.Contains("mDNS multicast interface error", StringComparison.OrdinalIgnoreCase)
        || line.Contains("mDNS IPv4 socket error", StringComparison.OrdinalIgnoreCase)
        || line.Contains("dnssd_register_raop failed", StringComparison.OrdinalIgnoreCase)
        || line.Contains("dnssd_register_airplay failed", StringComparison.OrdinalIgnoreCase);

    private static AppSettings CopySettings(AppSettings settings) => new()
    {
        ReceiverName = settings.ReceiverName,
        RequirePin = settings.RequirePin,
        Fullscreen = settings.Fullscreen,
        RunInBackground = settings.RunInBackground,
        Quality = settings.Quality,
        PlaybackProfile = settings.PlaybackProfile,
        StreamingDefaultsVersion = settings.StreamingDefaultsVersion,
        NetworkAdapterId = settings.NetworkAdapterId,
        PortStart = settings.PortStart,
        AllowPublicNetworks = settings.AllowPublicNetworks
    };

    private void ChangeState(ReceiverState state, string message) =>
        StateChanged?.Invoke(state, message);

    private readonly record struct VideoPipelineConfiguration(
        string EffectiveQuality,
        bool UseD3D11HardwareDecoding,
        string StatusMessage);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shouldRun = false;
        CancelRecovery();
        UnsubscribeFromNetworkChanges();

        Process? process;
        lock (_stateLock)
        {
            process = _process;
            _process = null;
        }

        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process already exited.
        }
        finally
        {
            process?.Dispose();
        }
    }

    [GeneratedRegex(@"PIN\s*=\s*[""']?(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex PinPattern();

    private sealed class NetworkUnavailableException : InvalidOperationException
    {
        public NetworkUnavailableException()
            : base("Es wurde keine aktive IPv4-LAN-Verbindung gefunden.")
        {
        }
    }
}
