using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LocalPlay.Models;
using LocalPlay.Services;

namespace LocalPlay;

public partial class MainWindow : Window
{
    private static readonly Brush IdleBrush = new SolidColorBrush(Color.FromRgb(125, 136, 156));
    private static readonly Brush ReadyBrush = new SolidColorBrush(Color.FromRgb(62, 201, 142));
    private static readonly Brush PairingBrush = new SolidColorBrush(Color.FromRgb(255, 167, 38));
    private static readonly Brush FaultBrush = new SolidColorBrush(Color.FromRgb(235, 87, 87));
    private static readonly Brush ActiveNavigationBrush =
        new SolidColorBrush(Color.FromRgb(40, 88, 198));

    private readonly SettingsStore _settingsStore = new();
    private readonly AirPlayEngine _engine = new();
    private IReadOnlyList<NetworkAdapterOption> _networkAdapters = [];
    private bool _loadingNetworkSettings;
    private bool _closing;

    public MainWindow()
    {
        InitializeComponent();
        PopulateNetworkAdapters();
        LoadSettings();

        HostNameText.Text = Environment.MachineName;
        UpdateNetworkSummary();

        _engine.LogReceived += line => Dispatcher.Invoke(() => AppendLog(line));
        _engine.PinReceived += pin => Dispatcher.Invoke(() =>
        {
            PinText.Text = $"PIN  {pin}";
            PinText.Visibility = Visibility.Visible;
        });
        _engine.StateChanged += (state, message) =>
            Dispatcher.Invoke(() => ApplyState(state, message));
    }

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.IsRunning)
        {
            StartStopButton.IsEnabled = false;
            await _engine.StopAsync();
            StartStopButton.IsEnabled = true;
            return;
        }

        var receiverName = ReceiverNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(receiverName))
        {
            MessageBox.Show(this, "Bitte gib dem AirPlay-Empfänger einen Namen.",
                "Name fehlt", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryValidateNetworkSettings(out var errorMessage))
        {
            ShowNetworkSettingsError(errorMessage);
            return;
        }

        var settings = ReadSettings();
        var adapter = NetworkInfoService.ResolveAdapter(_networkAdapters, settings.NetworkAdapterId);
        if (adapter is null || !await EnsureFirewallReadyForStartAsync(settings, adapter))
        {
            return;
        }

        _settingsStore.Save(settings);
        PinText.Visibility = Visibility.Collapsed;
        AppendLog(
            $"Starte „{settings.ReceiverName}“ auf {adapter?.IPv4Address ?? "keiner LAN-Adresse"}, " +
            $"Ports {settings.PortStart}–{settings.PortStart + 2} …");

        try
        {
            StartStopButton.IsEnabled = false;
            await _engine.StartAsync(settings, _settingsStore.PairingRegisterPath);
        }
        catch (Exception exception)
        {
            ApplyState(ReceiverState.Faulted, "Start fehlgeschlagen");
            MessageBox.Show(this, exception.Message, "LocalPlay",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            StartStopButton.IsEnabled = true;
        }
    }

    private async void FirewallButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryValidateNetworkSettings(out var errorMessage))
        {
            ShowNetworkSettingsError(errorMessage);
            return;
        }

        NetworkFirewallButton.IsEnabled = false;
        FirewallButton.IsEnabled = false;
        try
        {
            var settings = ReadSettings();
            _settingsStore.Save(settings);
            AppendLog("Windows fragt nach Administratorrechten für die LAN-Freigabe.");
            var result = await FirewallService.ConfigureLocalNetworkRulesAsync(
                settings.PortStart,
                settings.AllowPublicNetworks);
            SetNetworkTestResult(result.IsSuccessful, result.IsSuccessful
                ? "Firewall ist eingerichtet"
                : "Firewall-Einrichtung fehlgeschlagen", result.Message);
            AppendLog($"Firewall: {result.Message}");
            MessageBox.Show(
                this,
                result.Message,
                "Firewall-Freigabe",
                MessageBoxButton.OK,
                result.IsSuccessful
                    ? MessageBoxImage.Information
                    : result.WasCanceled
                        ? MessageBoxImage.Warning
                        : MessageBoxImage.Error);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Firewall-Freigabe",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            NetworkFirewallButton.IsEnabled = true;
            FirewallButton.IsEnabled = true;
        }
    }

    private void ReceiverNavButton_Click(object sender, RoutedEventArgs e) =>
        ShowPage(showNetwork: false);

    private void NetworkNavButton_Click(object sender, RoutedEventArgs e) =>
        ShowPage(showNetwork: true);

    private void ShowPage(bool showNetwork)
    {
        ReceiverPage.Visibility = showNetwork ? Visibility.Collapsed : Visibility.Visible;
        NetworkPage.Visibility = showNetwork ? Visibility.Visible : Visibility.Collapsed;
        ReceiverNavButton.Background = showNetwork ? Brushes.Transparent : ActiveNavigationBrush;
        NetworkNavButton.Background = showNetwork ? ActiveNavigationBrush : Brushes.Transparent;
    }

    private void SaveNetworkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryValidateNetworkSettings(out var errorMessage))
        {
            ShowNetworkSettingsError(errorMessage);
            return;
        }

        var settings = ReadSettings();
        _settingsStore.Save(settings);
        UpdateNetworkSummary();
        SetNetworkTestResult(
            true,
            "Einstellungen gespeichert",
            "Die Auswahl wird beim nächsten Start des Empfängers verwendet.");
        AppendLog("Netzwerkeinstellungen gespeichert.");
    }

    private async void TestNetworkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryValidateNetworkSettings(out var errorMessage))
        {
            SetNetworkTestResult(false, "Einstellungen prüfen", errorMessage);
            return;
        }

        TestNetworkButton.IsEnabled = false;
        try
        {
            var settings = ReadSettings();
            var adapters = _networkAdapters.ToArray();
            var engineIsRunning = _engine.IsRunning;
            var result = await Task.Run(() => NetworkDiagnosticsService.Run(
                settings,
                adapters,
                engineIsRunning));
            SetNetworkTestResult(result.IsSuccessful, result.Title, result.Details);
            AppendLog($"Netzwerktest: {result.Title}. {result.Details}");
        }
        finally
        {
            TestNetworkButton.IsEnabled = true;
        }
    }

    private void RefreshAdaptersButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedId = (NetworkAdapterComboBox.SelectedItem as NetworkAdapterOption)?.Id;
        PopulateNetworkAdapters(selectedId);
        UpdateNetworkSummary();
        SetNetworkTestResult(
            true,
            "Adapterliste aktualisiert",
            "Die aktiven IPv4-Schnittstellen wurden neu eingelesen.");
    }

    private void NetworkAdapterComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_loadingNetworkSettings)
        {
            return;
        }

        UpdateNetworkSummary();
        ResetNetworkTest();
    }

    private void PortStartTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (PortRangeText is null)
        {
            return;
        }

        if (int.TryParse(PortStartTextBox.Text, out var portStart)
            && portStart is >= 1024 and <= 65533)
        {
            PortRangeText.Text = $"{portStart}–{portStart + 2}";
            PortRangeText.Foreground = (Brush)FindResource("Ink");
        }
        else
        {
            PortRangeText.Text = "Ungültiger Bereich";
            PortRangeText.Foreground = FaultBrush;
        }

        if (!_loadingNetworkSettings)
        {
            ResetNetworkTest();
        }
    }

    private void AllowPublicNetworksCheckBox_Click(object sender, RoutedEventArgs e)
    {
        UpdateFirewallProfileSummary();
        ResetNetworkTest();
    }

    private void PopulateNetworkAdapters(string? preferredId = null)
    {
        _loadingNetworkSettings = true;
        try
        {
            _networkAdapters = NetworkInfoService.GetAdapters();
            NetworkAdapterComboBox.ItemsSource = _networkAdapters;
            NetworkAdapterComboBox.SelectedItem = _networkAdapters.FirstOrDefault(
                    option => string.Equals(
                        option.Id,
                        preferredId,
                        StringComparison.OrdinalIgnoreCase))
                ?? _networkAdapters.First();
        }
        finally
        {
            _loadingNetworkSettings = false;
        }
    }

    private void LoadSettings()
    {
        var settings = _settingsStore.Load();
        ReceiverNameTextBox.Text = settings.ReceiverName;
        PinCheckBox.IsChecked = settings.RequirePin;
        FullscreenCheckBox.IsChecked = settings.Fullscreen;
        AllowPublicNetworksCheckBox.IsChecked = settings.AllowPublicNetworks;
        UpdateFirewallProfileSummary();

        QualityComboBox.SelectedIndex = settings.Quality switch
        {
            "1080p · 60 FPS" => 1,
            "2K · 30 FPS (HEVC)" => 2,
            "2K · 60 FPS (HEVC)" => 3,
            "4K · 30 FPS (HEVC)" => 4,
            "4K · 60 FPS (HEVC)" => 5,
            _ => 0
        };

        _loadingNetworkSettings = true;
        try
        {
            NetworkAdapterComboBox.SelectedItem = _networkAdapters.FirstOrDefault(
                    option => string.Equals(
                        option.Id,
                        settings.NetworkAdapterId,
                        StringComparison.OrdinalIgnoreCase))
                ?? _networkAdapters.First();
            PortStartTextBox.Text =
                (settings.PortStart is >= 1024 and <= 65533 ? settings.PortStart : 7000)
                .ToString();
        }
        finally
        {
            _loadingNetworkSettings = false;
        }
    }

    private AppSettings ReadSettings()
    {
        var selectedAdapter = NetworkAdapterComboBox.SelectedItem as NetworkAdapterOption;
        var portStart = int.TryParse(PortStartTextBox.Text, out var parsedPort)
            && parsedPort is >= 1024 and <= 65533
                ? parsedPort
                : 7000;

        return new AppSettings
        {
            ReceiverName = ReceiverNameTextBox.Text.Trim(),
            RequirePin = PinCheckBox.IsChecked == true,
            Fullscreen = FullscreenCheckBox.IsChecked == true,
            Quality = (QualityComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? "1080p · 30 FPS",
            NetworkAdapterId = selectedAdapter?.IsAutomatic == false
                ? selectedAdapter.Id
                : string.Empty,
            PortStart = portStart,
            AllowPublicNetworks = AllowPublicNetworksCheckBox.IsChecked == true
        };
    }

    private async Task<bool> EnsureFirewallReadyForStartAsync(
        AppSettings settings,
        NetworkAdapterOption adapter)
    {
        var networkCategory = await Task.Run(
            () => WindowsNetworkProfileService.GetCategory(adapter.InterfaceIndex));
        if (networkCategory == WindowsNetworkCategory.Public
            && !settings.AllowPublicNetworks)
        {
            const string message =
                "Windows stuft diese Verbindung als öffentlich ein. Öffne „Netzwerk“ und " +
                "erlaube öffentliche Windows-Netzwerke, wenn du diesem LAN vertraust.";
            ShowPage(showNetwork: true);
            SetNetworkTestResult(false, "Öffentliches Netzwerk ist blockiert", message);
            MessageBox.Show(this, message, "LocalPlay",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (networkCategory == WindowsNetworkCategory.Unknown)
        {
            const string message =
                "Das Windows-Netzwerkprofil des ausgewählten Adapters konnte nicht erkannt werden. " +
                "Wähle den echten Ethernet- oder WLAN-Adapter.";
            ShowNetworkSettingsError(message);
            return false;
        }

        var status = await Task.Run(() => FirewallService.GetRuleStatus(
            settings.PortStart,
            settings.AllowPublicNetworks));
        if (status == FirewallRuleStatus.Ready)
        {
            return true;
        }

        var answer = MessageBox.Show(
            this,
            "LocalPlay benötigt einmalig passende Windows-Firewall-Regeln. " +
            "Sie erlauben nur die gewählten Ports und mDNS aus dem lokalen Subnetz.\n\n" +
            "Jetzt mit Administratorrechten einrichten?",
            "Firewall einrichten",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (answer != MessageBoxResult.Yes)
        {
            ShowPage(showNetwork: true);
            SetNetworkTestResult(
                false,
                "Firewall-Freigabe erforderlich",
                "Ohne die LAN-Freigabe können andere Geräte diesen PC nicht erreichen.");
            return false;
        }

        var result = await FirewallService.ConfigureLocalNetworkRulesAsync(
            settings.PortStart,
            settings.AllowPublicNetworks);
        SetNetworkTestResult(
            result.IsSuccessful,
            result.IsSuccessful ? "Firewall ist eingerichtet" : "Firewall-Einrichtung fehlgeschlagen",
            result.Message);
        AppendLog($"Firewall: {result.Message}");
        if (!result.IsSuccessful)
        {
            ShowPage(showNetwork: true);
            if (!result.WasCanceled)
            {
                MessageBox.Show(this, result.Message, "Firewall-Freigabe",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        return result.IsSuccessful;
    }

    private bool TryValidateNetworkSettings(out string errorMessage)
    {
        if (!int.TryParse(PortStartTextBox.Text, out var portStart)
            || portStart is < 1024 or > 65533)
        {
            errorMessage = "Der Startport muss eine Zahl zwischen 1024 und 65533 sein.";
            return false;
        }

        var selected = NetworkAdapterComboBox.SelectedItem as NetworkAdapterOption;
        var adapter = NetworkInfoService.ResolveAdapter(
            _networkAdapters,
            selected?.IsAutomatic == false ? selected.Id : string.Empty);
        if (adapter is null)
        {
            errorMessage = "Es wurde keine aktive IPv4-LAN-Verbindung gefunden.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private void ShowNetworkSettingsError(string message)
    {
        ShowPage(showNetwork: true);
        SetNetworkTestResult(false, "Einstellungen prüfen", message);
        MessageBox.Show(this, message, "Netzwerkeinstellungen",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void UpdateNetworkSummary()
    {
        var selected = NetworkAdapterComboBox.SelectedItem as NetworkAdapterOption;
        var adapter = NetworkInfoService.ResolveAdapter(
            _networkAdapters,
            selected?.IsAutomatic == false ? selected.Id : string.Empty);

        if (adapter is null)
        {
            SelectedAdapterTitleText.Text = "Keine aktive LAN-Verbindung";
            SelectedAdapterDetailsText.Text =
                "Verbinde diesen PC mit Ethernet oder WLAN und aktualisiere die Liste.";
            IpAddressText.Text = "Keine LAN-Adresse";
            return;
        }

        SelectedAdapterTitleText.Text = selected?.IsAutomatic != false
            ? $"Automatisch: {adapter.Name}"
            : adapter.Name;
        SelectedAdapterDetailsText.Text =
            $"{adapter.Kind} · IPv4 {adapter.IPv4Address} · " +
            (adapter.HasGateway ? "Gateway erkannt" : "ohne Standard-Gateway");
        IpAddressText.Text = $"{adapter.IPv4Address} · privates LAN";
    }

    private void ResetNetworkTest()
    {
        NetworkTestDot.Fill = IdleBrush;
        NetworkTestStatusText.Text = "Änderung noch nicht geprüft";
        NetworkTestDetailsText.Text =
            "Speichere die Auswahl oder starte einen neuen Verbindungstest.";
    }

    private void UpdateFirewallProfileSummary()
    {
        if (FirewallProfilesText is null)
        {
            return;
        }

        FirewallProfilesText.Text = AllowPublicNetworksCheckBox.IsChecked == true
            ? "Private + Domain + Public"
            : "Private + Domain";
    }

    private void SetNetworkTestResult(bool successful, string title, string details)
    {
        NetworkTestDot.Fill = successful ? ReadyBrush : FaultBrush;
        NetworkTestStatusText.Text = title;
        NetworkTestDetailsText.Text = details;
    }

    private void ApplyState(ReceiverState state, string message)
    {
        var brush = state switch
        {
            ReceiverState.Ready or ReceiverState.Streaming => ReadyBrush,
            ReceiverState.Pairing or ReceiverState.Starting => PairingBrush,
            ReceiverState.Faulted => FaultBrush,
            _ => IdleBrush
        };

        SidebarStatusDot.Fill = brush;
        MainStatusDot.Fill = brush;
        SidebarStatusText.Text = state switch
        {
            ReceiverState.Ready => "Bereit",
            ReceiverState.Pairing => "Kopplung",
            ReceiverState.Streaming => "Verbunden",
            ReceiverState.Starting => "Startet",
            ReceiverState.Faulted => "Fehler",
            _ => "Empfänger aus"
        };
        MainStatusText.Text = message;

        var running = state is ReceiverState.Starting
            or ReceiverState.Ready
            or ReceiverState.Pairing
            or ReceiverState.Streaming;
        StartStopButton.Content = running ? "Empfänger stoppen" : "Empfänger starten";
        ReceiverNameTextBox.IsEnabled = !running;
        QualityComboBox.IsEnabled = !running;
        PinCheckBox.IsEnabled = !running;
        FullscreenCheckBox.IsEnabled = !running;
        NetworkAdapterComboBox.IsEnabled = !running;
        PortStartTextBox.IsEnabled = !running;
        AllowPublicNetworksCheckBox.IsEnabled = !running;
        SaveNetworkButton.IsEnabled = !running;

        if (!running)
        {
            PinText.Visibility = Visibility.Collapsed;
        }
    }

    private void AppendLog(string line)
    {
        const int maximumCharacters = 16000;
        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
        if (LogTextBox.Text.Length > maximumCharacters)
        {
            LogTextBox.Text = LogTextBox.Text[^maximumCharacters..];
        }

        LogTextBox.ScrollToEnd();
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_closing)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        _closing = true;
        _settingsStore.Save(ReadSettings());
        await _engine.StopAsync();
        _engine.Dispose();
        Close();
    }
}
