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

    private readonly SettingsStore _settingsStore = new();
    private readonly AirPlayEngine _engine = new();
    private bool _closing;

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();

        HostNameText.Text = Environment.MachineName;
        IpAddressText.Text = $"{NetworkInfoService.GetLocalAddress()} · privates LAN";

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

        var settings = ReadSettings();
        _settingsStore.Save(settings);
        PinText.Visibility = Visibility.Collapsed;
        AppendLog($"Starte „{settings.ReceiverName}“ auf 7000–7002 …");

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

    private void FirewallButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            FirewallService.RequestPrivateLanRule();
            AppendLog("Windows fragt nach Administratorrechten für die private LAN-Freigabe.");
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Firewall-Freigabe",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadSettings()
    {
        var settings = _settingsStore.Load();
        ReceiverNameTextBox.Text = settings.ReceiverName;
        PinCheckBox.IsChecked = settings.RequirePin;
        FullscreenCheckBox.IsChecked = settings.Fullscreen;

        QualityComboBox.SelectedIndex = settings.Quality switch
        {
            "1080p · 60 FPS" => 1,
            "2K · 30 FPS (HEVC)" => 2,
            "2K · 60 FPS (HEVC)" => 3,
            "4K · 30 FPS (HEVC)" => 4,
            "4K · 60 FPS (HEVC)" => 5,
            _ => 0
        };
    }

    private AppSettings ReadSettings() => new()
    {
        ReceiverName = ReceiverNameTextBox.Text.Trim(),
        RequirePin = PinCheckBox.IsChecked == true,
        Fullscreen = FullscreenCheckBox.IsChecked == true,
        Quality = (QualityComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? "1080p · 30 FPS"
    };

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
