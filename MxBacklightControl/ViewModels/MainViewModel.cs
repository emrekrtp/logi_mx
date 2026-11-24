using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MxBacklightControl.Hid;
using MxBacklightControl.Models;
using MxBacklightControl.Services;

namespace MxBacklightControl.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly LogitechBacklightService _backlightService;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;
    private readonly TrayIconService _trayService;
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly AppSettings _settings;

    private KeyboardState _keyboardState = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? ShowWindowRequested;
    public event Action? ExitRequested;

    public LocalizationService Loc => LocalizationService.Instance;

    public ICommand SetAlwaysOnCommand { get; }
    public ICommand SetAlwaysOffCommand { get; }
    public ICommand SetAutoCommand { get; }
    public ICommand ReconnectCommand { get; }
    public ICommand ToggleLanguageCommand { get; }

    public MainViewModel()
    {
        _settingsService = new SettingsService();
        _startupService = new StartupService();
        _settings = _settingsService.Load();
        _settings.LaunchAtStartup = _startupService.IsEnabled();

        Loc.SetLanguage(LocalizationService.ParseLanguageCode(_settings.Language));

        _backlightService = new LogitechBacklightService();
        _trayService = new TrayIconService();
        _hotkeyService = new GlobalHotkeyService();

        SetAlwaysOnCommand = new RelayCommand(_ => SelectedMode = BacklightMode.AlwaysOn);
        SetAlwaysOffCommand = new RelayCommand(_ => SelectedMode = BacklightMode.AlwaysOff);
        SetAutoCommand = new RelayCommand(_ => SelectedMode = BacklightMode.Auto);
        ReconnectCommand = new RelayCommand(_ => _backlightService.RequestReconnect());
        ToggleLanguageCommand = new RelayCommand(_ => ToggleLanguage());

        Loc.LanguageChanged += OnLanguageChanged;
        _backlightService.StateChanged += OnKeyboardStateChanged;

        _trayService.ModeSelected += mode => RunOnUi(() => SelectedMode = mode);
        _trayService.ShowWindowRequested += () => RunOnUi(() => ShowWindowRequested?.Invoke());
        _trayService.ExitRequested += () => RunOnUi(() => ExitRequested?.Invoke());

        _hotkeyService.HotkeyPressed += () => RunOnUi(CycleAlwaysOnOff);

        _trayService.UpdateState(SelectedMode, _keyboardState);
        _backlightService.Start(_settings);
    }

    private void ToggleLanguage()
    {
        Loc.ToggleLanguage();
        _settings.Language = Loc.LanguageCode;
        _settingsService.Save(_settings);
    }

    private void OnLanguageChanged(AppLanguage _)
    {
        RunOnUi(() =>
        {
            _trayService.RefreshLocalization();
            OnPropertyChanged(nameof(ActiveModeTitle));
            OnPropertyChanged(nameof(ActiveModeSubtitle));
            OnPropertyChanged(nameof(KeepAliveLabel));
            OnPropertyChanged(nameof(DeviceName));
            OnPropertyChanged(nameof(ConnectionLabel));
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(BatteryLabel));
            OnPropertyChanged(nameof(ProtocolLabel));
            OnPropertyChanged(nameof(HardwareLevelLabel));
            OnPropertyChanged(nameof(BrightnessHintText));
        });
    }

    public void AttachWindow(Window window)
    {
        _hotkeyService.Attach(window, _settings.EnableGlobalHotkey);
    }

    public bool StartMinimized => _settings.StartMinimized;

    public BacklightMode SelectedMode
    {
        get => _settings.Mode;
        set
        {
            if (_settings.Mode == value)
            {
                return;
            }
            _settings.Mode = value;
            _settingsService.Save(_settings);
            _backlightService.SetMode(value);
            _trayService.UpdateState(value, _keyboardState);

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAlwaysOn));
            OnPropertyChanged(nameof(IsAlwaysOff));
            OnPropertyChanged(nameof(IsAuto));
            OnPropertyChanged(nameof(ActiveModeTitle));
            OnPropertyChanged(nameof(ActiveModeSubtitle));
        }
    }

    public bool IsAlwaysOn => SelectedMode == BacklightMode.AlwaysOn;
    public bool IsAlwaysOff => SelectedMode == BacklightMode.AlwaysOff;
    public bool IsAuto => SelectedMode == BacklightMode.Auto;

    public string ActiveModeTitle => SelectedMode switch
    {
        BacklightMode.AlwaysOn => Loc.AlwaysOnTitle,
        BacklightMode.AlwaysOff => Loc.AlwaysOffTitle,
        _ => Loc.AutoFullTitle
    };

    public string ActiveModeSubtitle => SelectedMode switch
    {
        BacklightMode.AlwaysOn => Loc.AlwaysOnBanner,
        BacklightMode.AlwaysOff => Loc.AlwaysOffBanner,
        _ => Loc.AutoBanner
    };

    public int KeepAliveIntervalSeconds
    {
        get => _settings.KeepAliveIntervalSeconds;
        set
        {
            int clamped = Math.Clamp(value, 2, 180);
            if (_settings.KeepAliveIntervalSeconds == clamped)
            {
                return;
            }
            _settings.KeepAliveIntervalSeconds = clamped;
            _settingsService.Save(_settings);
            _backlightService.UpdateKeepAliveInterval(clamped);
            OnPropertyChanged();
            OnPropertyChanged(nameof(KeepAliveLabel));
        }
    }

    public string KeepAliveLabel => Loc.FormatKeepAlive(KeepAliveIntervalSeconds);

    public int ManualBrightnessLevel
    {
        get => _settings.ManualBrightnessLevel;
        set
        {
            int clamped = Math.Clamp(value, 1, 7);
            if (_settings.ManualBrightnessLevel == clamped)
            {
                return;
            }
            _settings.ManualBrightnessLevel = clamped;
            _settingsService.Save(_settings);
            _backlightService.UpdateManualBrightness(clamped);
            OnPropertyChanged();
        }
    }

    public bool MinimizeToTray
    {
        get => _settings.MinimizeToTray;
        set
        {
            if (_settings.MinimizeToTray == value) return;
            _settings.MinimizeToTray = value;
            _settingsService.Save(_settings);
            OnPropertyChanged();
        }
    }

    public bool LaunchAtStartup
    {
        get => _settings.LaunchAtStartup;
        set
        {
            if (_settings.LaunchAtStartup == value) return;
            _settings.LaunchAtStartup = value;
            _startupService.SetEnabled(value);
            _settingsService.Save(_settings);
            OnPropertyChanged();
        }
    }

    public bool EnableGlobalHotkey
    {
        get => _settings.EnableGlobalHotkey;
        set
        {
            if (_settings.EnableGlobalHotkey == value) return;
            _settings.EnableGlobalHotkey = value;
            _hotkeyService.SetEnabled(value);
            _settingsService.Save(_settings);
            OnPropertyChanged();
        }
    }

    public bool IsConnected => _keyboardState.IsConnected;

    public string DeviceName => _keyboardState.IsConnected && !string.IsNullOrWhiteSpace(_keyboardState.DeviceName)
        ? _keyboardState.DeviceName
        : _keyboardState.StatusKind switch
        {
            KeyboardStatusKind.Searching => Loc.DeviceSearching,
            KeyboardStatusKind.NotFound => Loc.DeviceNotFound,
            _ => Loc.DeviceDisconnected
        };

    public string ConnectionLabel => _keyboardState.ConnectionType switch
    {
        ConnectionType.BluetoothLE => "Bluetooth LE",
        ConnectionType.LogiBolt => "Logi Bolt",
        ConnectionType.Unifying => "Unifying",
        ConnectionType.UsbDirect => "USB",
        _ => Loc.DisconnectedLabel
    };

    public string StatusMessage => _keyboardState.StatusKind switch
    {
        KeyboardStatusKind.Searching => Loc.StatusSearching,
        KeyboardStatusKind.Reconnecting => Loc.StatusReconnecting,
        KeyboardStatusKind.DisconnectedRescanning => Loc.StatusDisconnectedRescanning,
        KeyboardStatusKind.SleepingOrDisconnected => Loc.StatusSleepingOrDisconnected,
        KeyboardStatusKind.NotFound => Loc.StatusNotFound,
        KeyboardStatusKind.CommunicationError => Loc.FormatCommunicationError(_keyboardState.ErrorDetail),
        KeyboardStatusKind.ModeAlwaysOn => Loc.StatusModeAlwaysOn,
        KeyboardStatusKind.ModeAlwaysOff => Loc.StatusModeAlwaysOff,
        _ => Loc.StatusModeAuto
    };

    public string BatteryLabel => !_keyboardState.IsConnected
        ? "--"
        : !_keyboardState.HasBattery
            ? Loc.UnknownLabel
            : Loc.FormatBattery(_keyboardState.BatteryPercent, _keyboardState.IsCharging);

    public string ProtocolLabel => !_keyboardState.IsConnected
        ? Loc.WaitingConnection
        : $"HID++ {_keyboardState.ProtocolMajor}.{_keyboardState.ProtocolMinor} • 0x1982 v{_keyboardState.BacklightFeatureVersion}";

    public string HardwareLevelLabel => !_keyboardState.IsConnected
        ? "--"
        : !_keyboardState.HardwareBacklightEnabled
            ? Loc.BacklightOffZero
            : Loc.FormatLevel(_keyboardState.CurrentBrightnessLevel, _keyboardState.MaxBrightnessLevels);

    public bool SupportsManualBrightnessSlider => _keyboardState.IsConnected && _keyboardState.BacklightFeatureVersion >= 3;

    public int DisplayBrightnessLevel
    {
        get => SupportsManualBrightnessSlider
            ? ManualBrightnessLevel
            : (_keyboardState.HardwareBacklightEnabled ? _keyboardState.CurrentBrightnessLevel : 0);
        set
        {
            if (SupportsManualBrightnessSlider)
            {
                ManualBrightnessLevel = value;
                OnPropertyChanged();
            }
        }
    }

    public string BrightnessHintText => !_keyboardState.IsConnected
        ? Loc.BrightnessHintDisconnected
        : SupportsManualBrightnessSlider
            ? Loc.BrightnessHintInteractive
            : Loc.BrightnessHintReadOnly;

    private void CycleAlwaysOnOff()
    {
        // Quick toggle between AlwaysOn and AlwaysOff via Ctrl+Alt+L
        SelectedMode = SelectedMode == BacklightMode.AlwaysOn
            ? BacklightMode.AlwaysOff
            : BacklightMode.AlwaysOn;
    }

    private void OnKeyboardStateChanged(KeyboardState state)
    {
        RunOnUi(() =>
        {
            _keyboardState = state;
            _trayService.UpdateState(SelectedMode, state);

            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(DeviceName));
            OnPropertyChanged(nameof(ConnectionLabel));
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(BatteryLabel));
            OnPropertyChanged(nameof(ProtocolLabel));
            OnPropertyChanged(nameof(HardwareLevelLabel));
            OnPropertyChanged(nameof(SupportsManualBrightnessSlider));
            OnPropertyChanged(nameof(DisplayBrightnessLevel));
            OnPropertyChanged(nameof(BrightnessHintText));
        });
    }

    private static void RunOnUi(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        if (app.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            app.Dispatcher.BeginInvoke(action);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        Loc.LanguageChanged -= OnLanguageChanged;
        _hotkeyService.Dispose();
        _trayService.Dispose();
        _backlightService.Dispose();
    }
}
