using System.ComponentModel;
using System.Globalization;

namespace MxBacklightControl.Services;

public enum AppLanguage
{
    English,
    Turkish
}

public sealed class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    private AppLanguage _currentLanguage = DetectDefaultLanguage();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<AppLanguage>? LanguageChanged;

    private LocalizationService()
    {
    }

    public AppLanguage CurrentLanguage => _currentLanguage;
    public bool IsTurkish => _currentLanguage == AppLanguage.Turkish;
    public string LanguageCode => IsTurkish ? "tr" : "en";

    public static AppLanguage DetectDefaultLanguage()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("tr", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Turkish
            : AppLanguage.English;
    }

    public static AppLanguage ParseLanguageCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return DetectDefaultLanguage();
        }

        return code.Trim().ToLowerInvariant() switch
        {
            "tr" or "tr-tr" or "turkish" => AppLanguage.Turkish,
            "en" or "en-us" or "en-gb" or "english" => AppLanguage.English,
            _ => DetectDefaultLanguage()
        };
    }

    public void SetLanguage(AppLanguage language)
    {
        if (_currentLanguage == language)
        {
            return;
        }

        _currentLanguage = language;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        LanguageChanged?.Invoke(language);
    }

    public void ToggleLanguage()
    {
        SetLanguage(IsTurkish ? AppLanguage.English : AppLanguage.Turkish);
    }

    private string T(string en, string tr) => IsTurkish ? tr : en;

    // Title Bar
    public string AppSubtitle => T("Logitech HID++ Backlight Control", "Logitech HID++ Arka Işık Kontrolü");
    public string LanguageToggleLabel => IsTurkish ? "EN" : "TR";
    public string LanguageToggleTooltip => T("Türkçe'ye Geç (Switch to Turkish)", "Switch to English (İngilizce'ye Geç)");
    public string MinimizeTooltip => T("Minimize to Tray", "Tepsiye Küçült");
    public string CloseTooltip => T("Close", "Kapat");
    public string RescanTooltip => T("Rescan Keyboard", "Klavyeyi Yeniden Tara");

    // Telemetry Pills
    public string ConnectionPrefix => T("Connection: ", "Bağlantı: ");
    public string BatteryPrefix => T("Battery: ", "Pil: ");
    public string LightPrefix => T("Light: ", "Işık: ");
    public string DisconnectedLabel => T("Disconnected", "Bağlı Değil");
    public string UnknownLabel => T("Unknown", "Bilinmiyor");
    public string ChargingSuffix => T("Charging", "Şarj oluyor");
    public string WaitingConnection => T("Waiting for connection", "Bağlantı bekleniyor");
    public string BacklightOffZero => T("Off (0)", "Kapalı (0)");

    public string FormatBattery(int percent, bool isCharging)
    {
        if (IsTurkish)
        {
            return isCharging ? $"%{percent} ({ChargingSuffix})" : $"%{percent}";
        }
        return isCharging ? $"{percent}% ({ChargingSuffix})" : $"{percent}%";
    }

    public string FormatBatteryShort(int percent) => IsTurkish ? $"%{percent}" : $"{percent}%";
    public string FormatLevel(int current, int max) => IsTurkish ? $"Seviye {current} / {max}" : $"Level {current} / {max}";
    public string FormatKeepAlive(int seconds) => IsTurkish ? $"{seconds} sn" : $"{seconds} s";

    // Mode Section
    public string ModeSectionTitle => T("BACKLIGHT MODE", "ARKA IŞIK MODU");
    public string AlwaysOnTitle => T("Always On", "Sürekli Açık");
    public string AlwaysOnSub => T("Keep Awake", "Always On");
    public string AlwaysOffTitle => T("Always Off", "Sürekli Kapalı");
    public string AlwaysOffSub => T("Locked Off", "Always Off");
    public string AutoTitle => T("Auto", "Otomatik");
    public string AutoSub => T("Default", "Varsayılan");
    public string AutoFullTitle => T("Auto (Default)", "Otomatik (Varsayılan)");

    public string AlwaysOnBanner => T(
        "Keyboard backlight stays continuously on without timing out.",
        "Klavye ışığı zaman aşımına uğramadan sürekli açık tutulur.");
    public string AlwaysOffBanner => T(
        "Proximity sensor and keystrokes will not turn the backlight on; locked off.",
        "Yakınlık sensörü ve tuş basımı ışığı açamaz; tamamen kapalıdır.");
    public string AutoBanner => T(
        "Logitech's default proximity and ambient light sensor behavior is active.",
        "Logitech'in standart yakınlık ve ortam ışığı sensörü davranışı aktiftir.");

    // Settings Section
    public string SettingsSectionTitle => T("SETTINGS", "AYARLAR");
    public string KeepAliveTitle => T("Keep-Alive Refresh Interval (Always On)", "Canlı Tutma Yenileme Aralığı (Sürekli Açık)");
    public string KeepAliveHint => T(
        "Recommended: 3–4 s on battery, 60–180 s when connected via USB cable.",
        "Pil modunda 3–4 sn, USB kablo takılıyken 60–180 sn önerilir.");
    public string BrightnessTitle => T("Brightness Level", "Parlaklık Seviyesi");
    public string BrightnessHintDisconnected => T(
        "Brightness level will appear here once the keyboard is connected.",
        "Klavye bağlandığında parlaklık seviyesi burada görünür.");
    public string BrightnessHintInteractive => T(
        "Use the slider to directly adjust keyboard brightness (1–7).",
        "Kaydırıcı ile klavye parlaklığını doğrudan ayarlayabilirsiniz (1–7).");
    public string BrightnessHintReadOnly => T(
        "MX Keys (0x1982 v1) firmware does not allow software brightness control. Use F6 / F7 on the keyboard; the current level is shown live here.",
        "MX Keys (0x1982 v1) firmware'i parlaklığın yazılımdan değiştirilmesine izin vermez. Klavyedeki F6 / F7 tuşlarıyla ayarlayabilirsiniz; mevcut seviye burada canlı gösterilir.");

    public string MinimizeToTrayLabel => T("Keep running in system tray when closed", "Kapatınca sistem tepsisinde çalışmaya devam et");
    public string LaunchAtStartupLabel => T("Launch automatically at Windows startup", "Windows başlangıcında otomatik başlat");
    public string GlobalHotkeyLabel => T("Global shortcut key (Ctrl + Alt + L)", "Global kısayol tuşu (Ctrl + Alt + L)");

    // Device / Status messages
    public string DeviceSearching => T("Searching for keyboard...", "Klavye aranıyor...");
    public string DeviceNotFound => T("Logitech MX Keyboard Not Found", "Logitech MX Klavye Bulunamadı");
    public string DeviceDisconnected => T("Keyboard Disconnected", "Klavye Bağlı Değil");

    public string StatusSearching => T("Waiting for Logitech MX keyboard...", "Logitech MX klavye bekleniyor...");
    public string StatusReconnecting => T("Reconnecting to keyboard...", "Klavye yeniden bağlanıyor...");
    public string StatusDisconnectedRescanning => T("Keyboard disconnected, rescanning...", "Klavye bağlantısı kesildi, yeniden aranıyor...");
    public string StatusSleepingOrDisconnected => T("Keyboard is sleeping or disconnected...", "Klavye uyku modunda veya bağlantısı kesildi...");
    public string StatusNotFound => T("No keyboard connected via Bluetooth, Logi Bolt, or Unifying.", "Klavye Bluetooth, Logi Bolt veya Unifying ile bağlı değil.");
    public string FormatCommunicationError(string? detail) => T($"Communication error: {detail}", $"İletişim hatası: {detail}");
    public string StatusModeAlwaysOn => T("Always On active — keeping backlight alive", "Sürekli Açık modu aktif — arka ışık canlı tutuluyor");
    public string StatusModeAlwaysOff => T("Always Off active — backlight locked off", "Sürekli Kapalı modu aktif — arka ışık kilitli kapalı");
    public string StatusModeAuto => T("Auto (Logitech default) — proximity & ambient sensors active", "Otomatik (Logitech varsayılanı) — yakınlık ve ortam sensörü aktif");

    // Tray Menu
    public string TrayAlwaysOn => T("☀  Always On", "☀  Sürekli Açık (Always On)");
    public string TrayAlwaysOff => T("🌙  Always Off", "🌙  Sürekli Kapalı (Always Off)");
    public string TrayAuto => T("✨  Auto (Default)", "✨  Otomatik (Varsayılan)");
    public string TrayShowWindow => T("Show Window", "Pencereyi Göster");
    public string TrayExit => T("Exit", "Çıkış");
    public string TrayNotConnected => T("Keyboard not connected", "Klavye bağlı değil");

    // Single Instance Dialog
    public string AlreadyRunningMessage => T(
        "MX Backlight Control is already running in the background.\nYou can open it from the system tray icon.",
        "MX Backlight Control zaten arka planda çalışıyor.\nSistem tepsisindeki (saat yanı) simgeden açabilirsiniz.");
}
