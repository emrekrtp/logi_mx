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

    public ICommand SetAlwaysOnCommand { get; }
    public ICommand SetAlwaysOffCommand { get; }
    public ICommand SetAutoCommand { get; }
    public ICommand ReconnectCommand { get; }

    public MainViewModel()
    {
        _settingsService = new SettingsService();
        _startupService = new StartupService();
        _settings = _settingsService.Load();
        _settings.LaunchAtStartup = _startupService.IsEnabled();

        _backlightService = new LogitechBacklightService();
        _trayService = new TrayIconService();
        _hotkeyService = new GlobalHotkeyService();

        SetAlwaysOnCommand = new RelayCommand(_ => SelectedMode = BacklightMode.AlwaysOn);
        SetAlwaysOffCommand = new RelayCommand(_ => SelectedMode = BacklightMode.AlwaysOff);
        SetAutoCommand = new RelayCommand(_ => SelectedMode = BacklightMode.Auto);
        ReconnectCommand = new RelayCommand(_ => _backlightService.RequestReconnect());

        _backlightService.StateChanged += OnKeyboardStateChanged;

        _trayService.ModeSelected += mode => RunOnUi(() => SelectedMode = mode);
        _trayService.ShowWindowRequested += () => RunOnUi(() => ShowWindowRequested?.Invoke());
        _trayService.ExitRequested += () => RunOnUi(() => ExitRequested?.Invoke());

        _hotkeyService.HotkeyPressed += () => RunOnUi(CycleAlwaysOnOff);

        _backlightService.Start(_settings);
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
        BacklightMode.AlwaysOn => "Sürekli Açık",
        BacklightMode.AlwaysOff => "Sürekli Kapalı",
        _ => "Otomatik (Varsayılan)"
    };

    public string ActiveModeSubtitle => SelectedMode switch
    {
        BacklightMode.AlwaysOn => "Klavye ışığı zaman aşımına uğramadan sürekli açık tutulur.",
        BacklightMode.AlwaysOff => "Yakınlık sensörü ve tuş basımı ışığı açamaz; tamamen kapalıdır.",
        _ => "Logitech'in standart yakınlık ve ortam ışığı sensörü davranışı aktiftir."
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

    public string KeepAliveLabel => $"{KeepAliveIntervalSeconds} sn";

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
    public string DeviceName => _keyboardState.DeviceName;
    public string ConnectionLabel => _keyboardState.ConnectionLabel;
    public string StatusMessage => _keyboardState.StatusMessage;

    public string BatteryLabel => !_keyboardState.IsConnected
        ? "--"
        : !_keyboardState.HasBattery
            ? "Bilinmiyor"
            : _keyboardState.IsCharging
                ? $"%{_keyboardState.BatteryPercent} (Şarj oluyor)"
                : $"%{_keyboardState.BatteryPercent}";

    public string ProtocolLabel => !_keyboardState.IsConnected
        ? "Bağlantı bekleniyor"
        : $"HID++ {_keyboardState.ProtocolMajor}.{_keyboardState.ProtocolMinor} • 0x1982 v{_keyboardState.BacklightFeatureVersion}";

    public string HardwareLevelLabel => !_keyboardState.IsConnected
        ? "--"
        : !_keyboardState.HardwareBacklightEnabled
            ? "Kapalı (0)"
            : $"Seviye {_keyboardState.CurrentBrightnessLevel} / {_keyboardState.MaxBrightnessLevels}";

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
        ? "Klavye bağlandığında parlaklık seviyesi burada görünür."
        : SupportsManualBrightnessSlider
            ? "Kaydırıcı ile klavye parlaklığını doğrudan ayarlayabilirsiniz (1–7)."
            : "MX Keys (0x1982 v1) firmware'i parlaklığın yazılımdan değiştirilmesine izin vermez. Klavyedeki F6 / F7 tuşlarıyla ayarlayabilirsiniz; mevcut seviye burada canlı gösterilir.";

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
        _hotkeyService.Dispose();
        _trayService.Dispose();
        _backlightService.Dispose();
    }
}
