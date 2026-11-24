using Microsoft.Win32;
using MxBacklightControl.Models;

namespace MxBacklightControl.Hid;

public sealed class LogitechBacklightService : IDisposable
{
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private CancellationTokenSource? _cts;
    private Task? _workerTask;
    private HidppKeyboardSession? _session;
    private bool _disposed;

    private BacklightMode _mode = BacklightMode.Auto;
    private int _keepAliveIntervalSeconds = 4;
    private int _manualBrightnessLevel = 5;
    private bool _forceApplyPending = true;
    private bool _forceReconnectPending;
    private DateTime _lastBatteryPoll = DateTime.MinValue;
    private DateTime _lastTelemetryPoll = DateTime.MinValue;
    private DateTime _lastEnforceTick = DateTime.MinValue;
    private int _consecutiveErrors;

    public KeyboardState CurrentState { get; private set; } = new();
    public event Action<KeyboardState>? StateChanged;

    public BacklightMode Mode
    {
        get { lock (_stateLock) return _mode; }
    }

    public LogitechBacklightService()
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public void Start(AppSettings initialSettings)
    {
        lock (_stateLock)
        {
            _mode = initialSettings.Mode;
            _keepAliveIntervalSeconds = Math.Clamp(initialSettings.KeepAliveIntervalSeconds, 2, 180);
            _manualBrightnessLevel = Math.Clamp(initialSettings.ManualBrightnessLevel, 1, 7);
            _forceApplyPending = true;
        }

        _cts = new CancellationTokenSource();
        _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
    }

    public void SetMode(BacklightMode mode)
    {
        lock (_stateLock)
        {
            _mode = mode;
            _forceApplyPending = true;
        }
        SignalWorker();
    }

    public void UpdateKeepAliveInterval(int seconds)
    {
        lock (_stateLock)
        {
            _keepAliveIntervalSeconds = Math.Clamp(seconds, 2, 180);
        }
        SignalWorker();
    }

    public void UpdateManualBrightness(int level)
    {
        lock (_stateLock)
        {
            _manualBrightnessLevel = Math.Clamp(level, 1, 7);
            if (_mode == BacklightMode.AlwaysOn)
            {
                _forceApplyPending = true;
            }
        }
        SignalWorker();
    }

    public void RequestReconnect()
    {
        lock (_stateLock)
        {
            _forceReconnectPending = true;
            _forceApplyPending = true;
        }
        SignalWorker();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            RequestReconnect();
        }
    }

    private void SignalWorker()
    {
        try
        {
            if (_wakeSignal.CurrentCount == 0)
            {
                _wakeSignal.Release();
            }
        }
        catch (SemaphoreFullException)
        {
            // Already signaled
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                bool needReconnect;
                lock (_stateLock)
                {
                    needReconnect = _forceReconnectPending;
                    _forceReconnectPending = false;
                }

                if (needReconnect)
                {
                    DisconnectSession("Klavye yeniden bağlanıyor...");
                }

                if (_session is null)
                {
                    TryConnect();
                    if (_session is null)
                    {
                        await WaitAsync(TimeSpan.FromSeconds(3), ct);
                        continue;
                    }
                }

                bool applyNow;
                BacklightMode targetMode;
                int keepAliveSec;
                int brightness;
                lock (_stateLock)
                {
                    applyNow = _forceApplyPending;
                    _forceApplyPending = false;
                    targetMode = _mode;
                    keepAliveSec = _keepAliveIntervalSeconds;
                    brightness = _manualBrightnessLevel;
                }

                var now = DateTime.UtcNow;
                bool shouldEnforce = applyNow || ShouldEnforceNow(targetMode, keepAliveSec, now);

                if (shouldEnforce)
                {
                    bool ok = ApplyModeToHardware(_session, targetMode, brightness);
                    if (!ok)
                    {
                        _consecutiveErrors++;
                        if (_consecutiveErrors >= 2)
                        {
                            DisconnectSession("Klavye bağlantısı kesildi, yeniden aranıyor...");
                            await WaitAsync(TimeSpan.FromSeconds(2), ct);
                            continue;
                        }
                    }
                    else
                    {
                        _consecutiveErrors = 0;
                        _lastEnforceTick = DateTime.UtcNow;

                        if (applyNow || (DateTime.UtcNow - _lastTelemetryPoll).TotalSeconds >= 5)
                        {
                            RefreshTelemetry(_session, targetMode, forceBattery: applyNow);
                            _lastTelemetryPoll = DateTime.UtcNow;
                        }
                    }
                }
                else if ((now - _lastBatteryPoll).TotalSeconds >= 45)
                {
                    if (!_session.Ping())
                    {
                        _consecutiveErrors++;
                        if (_consecutiveErrors >= 2)
                        {
                            DisconnectSession("Klavye uyku modunda veya bağlantısı kesildi...");
                            await WaitAsync(TimeSpan.FromSeconds(2), ct);
                            continue;
                        }
                    }
                    else
                    {
                        _consecutiveErrors = 0;
                        RefreshTelemetry(_session, targetMode, forceBattery: true);
                        _lastTelemetryPoll = DateTime.UtcNow;
                    }
                }

                TimeSpan sleepDuration = ComputeNextSleep(targetMode, keepAliveSec);
                await WaitAsync(sleepDuration, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                DisconnectSession($"İletişim hatası: {ex.Message}");
                try
                {
                    await WaitAsync(TimeSpan.FromSeconds(3), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private bool ShouldEnforceNow(BacklightMode mode, int keepAliveSec, DateTime now)
    {
        double elapsed = (now - _lastEnforceTick).TotalSeconds;
        return mode switch
        {
            BacklightMode.AlwaysOn => elapsed >= keepAliveSec - 0.15,
            BacklightMode.AlwaysOff => elapsed >= 9.85,
            _ => false
        };
    }

    private TimeSpan ComputeNextSleep(BacklightMode mode, int keepAliveSec)
    {
        double elapsed = (DateTime.UtcNow - _lastEnforceTick).TotalSeconds;
        return mode switch
        {
            BacklightMode.AlwaysOn => TimeSpan.FromSeconds(Math.Clamp(keepAliveSec - elapsed, 0.25, keepAliveSec)),
            BacklightMode.AlwaysOff => TimeSpan.FromSeconds(Math.Clamp(10.0 - elapsed, 0.5, 10.0)),
            _ => TimeSpan.FromSeconds(10)
        };
    }

    private void TryConnect()
    {
        var discovered = HidppKeyboardSession.Discover();
        if (discovered is null)
        {
            PublishState(new KeyboardState
            {
                IsConnected = false,
                DeviceName = "Logitech MX Klavye Bulunamadı",
                ConnectionType = ConnectionType.Unknown,
                StatusMessage = "Klavye Bluetooth, Logi Bolt veya Unifying ile bağlı değil."
            });
            return;
        }

        _session = discovered;
        _consecutiveErrors = 0;
        _lastBatteryPoll = DateTime.MinValue;

        lock (_stateLock)
        {
            _forceApplyPending = true;
        }

        RefreshTelemetry(_session, Mode, forceBattery: true);
    }

    private bool ApplyModeToHardware(HidppKeyboardSession session, BacklightMode mode, int manualBrightness)
    {
        byte supported = session.SupportedOptions != 0 ? session.SupportedOptions : (byte)0x05;

        switch (mode)
        {
            case BacklightMode.AlwaysOn:
            {
                if (session.BacklightFeatureVersion >= 3)
                {
                    // Feature 0x1982 v3: manual mode (0x10) + user level + max duration
                    byte opts = (byte)(0x10 | (supported & 0x01));
                    return session.SetBacklightConfig(
                        enabled: true,
                        options: opts,
                        manualLevel: (byte)Math.Clamp(manualBrightness, 1, 7),
                        durationUnits: 360);
                }
                else
                {
                    // Feature 0x1982 v0/v1/v2 (MX Keys):
                    // MX Keys firmware ignores repeated setBacklightConfig(1, opts) when already enabled
                    // and still fades out after ~5s on battery. Sending a rapid 0 -> 1 pulse resets the
                    // internal backlight timeout timer and immediately lights/keeps the LEDs on.
                    byte opts = (byte)(supported & ~0x04);
                    if (!session.SetBacklightConfig(enabled: false, options: 0x00))
                    {
                        return false;
                    }
                    return session.SetBacklightConfig(enabled: true, options: opts);
                }
            }

            case BacklightMode.AlwaysOff:
            {
                return session.SetBacklightConfig(enabled: false, options: 0x00, manualLevel: 0);
            }

            case BacklightMode.Auto:
            default:
            {
                if (session.BacklightFeatureVersion >= 3)
                {
                    return session.SetBacklightConfig(enabled: true, options: 0x00, manualLevel: (byte)manualBrightness, durationUnits: 6);
                }
                return session.SetBacklightConfig(enabled: true, options: supported);
            }
        }
    }

    private void RefreshTelemetry(HidppKeyboardSession session, BacklightMode mode, bool forceBattery)
    {
        var cfg = session.GetBacklightConfig();
        var levelInfo = session.GetBacklightLevelInfo();

        BatteryInfo? bat = null;
        if (forceBattery || (DateTime.UtcNow - _lastBatteryPoll).TotalSeconds >= 45)
        {
            bat = session.GetBatteryInfo();
            if (bat is not null)
            {
                _lastBatteryPoll = DateTime.UtcNow;
            }
        }

        string statusText = mode switch
        {
            BacklightMode.AlwaysOn => "Sürekli Açık modu aktif — arka ışık canlı tutuluyor",
            BacklightMode.AlwaysOff => "Sürekli Kapalı modu aktif — arka ışık kilitli kapalı",
            _ => "Otomatik (Logitech varsayılanı) — yakınlık ve ortam sensörü aktif"
        };

        var prev = CurrentState;
        var next = new KeyboardState
        {
            IsConnected = true,
            DeviceName = session.DeviceName,
            ConnectionType = session.ConnectionType,
            ProtocolMajor = session.ProtocolMajor,
            ProtocolMinor = session.ProtocolMinor,
            BacklightFeatureIndex = session.BacklightFeatureIndex,
            BacklightFeatureVersion = session.BacklightFeatureVersion,
            SupportedOptions = session.SupportedOptions,
            HardwareBacklightEnabled = cfg?.Enabled ?? prev.HardwareBacklightEnabled,
            HardwareOptions = cfg?.Options ?? prev.HardwareOptions,
            MaxBrightnessLevels = levelInfo is not null ? Math.Max(1, levelInfo.LevelCount - 1) : 7,
            CurrentBrightnessLevel = levelInfo?.CurrentLevel ?? (cfg?.Level ?? prev.CurrentBrightnessLevel),
            HasBattery = bat is not null || prev.HasBattery,
            BatteryPercent = bat?.Percent ?? prev.BatteryPercent,
            IsCharging = bat?.IsCharging ?? prev.IsCharging,
            StatusMessage = statusText,
            LastUpdated = DateTime.Now
        };

        PublishState(next);
    }

    private void DisconnectSession(string message)
    {
        if (_session is not null)
        {
            _session.Dispose();
            _session = null;
        }

        PublishState(new KeyboardState
        {
            IsConnected = false,
            DeviceName = "Klavye Bağlı Değil",
            ConnectionType = ConnectionType.Unknown,
            StatusMessage = message,
            LastUpdated = DateTime.Now
        });
    }

    private void PublishState(KeyboardState state)
    {
        CurrentState = state;
        StateChanged?.Invoke(state);
    }

    private async Task WaitAsync(TimeSpan delay, CancellationToken ct)
    {
        await _wakeSignal.WaitAsync(delay, ct);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        SystemEvents.PowerModeChanged -= OnPowerModeChanged;

        try
        {
            _cts?.Cancel();
            SignalWorker();
            _workerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }

        _session?.Dispose();
        _session = null;
        _cts?.Dispose();
        _wakeSignal.Dispose();
    }
}
