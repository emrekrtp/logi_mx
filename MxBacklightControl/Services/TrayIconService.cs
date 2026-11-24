using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MxBacklightControl.Models;

namespace MxBacklightControl.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _itemAlwaysOn;
    private readonly ToolStripMenuItem _itemAlwaysOff;
    private readonly ToolStripMenuItem _itemAuto;
    private readonly ToolStripMenuItem _itemStatus;
    private Icon? _currentIcon;
    private bool _disposed;

    public event Action<BacklightMode>? ModeSelected;
    public event Action? ShowWindowRequested;
    public event Action? ExitRequested;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public TrayIconService()
    {
        var menu = new ContextMenuStrip();

        _itemStatus = new ToolStripMenuItem("MX Backlight Control")
        {
            Enabled = false
        };

        _itemAlwaysOn = new ToolStripMenuItem("☀  Sürekli Açık (Always On)", null, (_, _) => ModeSelected?.Invoke(BacklightMode.AlwaysOn));
        _itemAlwaysOff = new ToolStripMenuItem("🌙  Sürekli Kapalı (Always Off)", null, (_, _) => ModeSelected?.Invoke(BacklightMode.AlwaysOff));
        _itemAuto = new ToolStripMenuItem("✨  Otomatik (Varsayılan)", null, (_, _) => ModeSelected?.Invoke(BacklightMode.Auto));

        var itemShow = new ToolStripMenuItem("Pencereyi Göster", null, (_, _) => ShowWindowRequested?.Invoke());
        var itemExit = new ToolStripMenuItem("Çıkış", null, (_, _) => ExitRequested?.Invoke());

        menu.Items.Add(_itemStatus);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_itemAlwaysOn);
        menu.Items.Add(_itemAlwaysOff);
        menu.Items.Add(_itemAuto);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(itemShow);
        menu.Items.Add(itemExit);

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "MX Backlight Control",
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowWindowRequested?.Invoke();
        UpdateState(BacklightMode.Auto, new KeyboardState());
    }

    public void UpdateState(BacklightMode mode, KeyboardState state)
    {
        if (_disposed)
        {
            return;
        }

        _itemAlwaysOn.Checked = mode == BacklightMode.AlwaysOn;
        _itemAlwaysOff.Checked = mode == BacklightMode.AlwaysOff;
        _itemAuto.Checked = mode == BacklightMode.Auto;

        string modeText = mode switch
        {
            BacklightMode.AlwaysOn => "Sürekli Açık",
            BacklightMode.AlwaysOff => "Sürekli Kapalı",
            _ => "Otomatik"
        };

        string batText = state.IsConnected && state.HasBattery ? $" • %{state.BatteryPercent}" : string.Empty;
        _itemStatus.Text = state.IsConnected
            ? $"{state.DeviceName}{batText}"
            : "Klavye bağlı değil";

        string tooltip = $"MX Backlight: {modeText}{batText}";
        _notifyIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;

        var newIcon = CreateTrayIcon(mode, state.IsConnected);
        var oldIcon = _currentIcon;
        _currentIcon = newIcon;
        _notifyIcon.Icon = newIcon;
        oldIcon?.Dispose();
    }

    private static Icon CreateTrayIcon(BacklightMode mode, bool isConnected)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        Color bgColor = !isConnected
            ? Color.FromArgb(255, 68, 72, 82)
            : mode switch
            {
                BacklightMode.AlwaysOn => Color.FromArgb(255, 0, 184, 212),
                BacklightMode.AlwaysOff => Color.FromArgb(255, 42, 46, 56),
                _ => Color.FromArgb(255, 86, 118, 224)
            };

        Color fgColor = !isConnected
            ? Color.FromArgb(255, 175, 180, 190)
            : mode == BacklightMode.AlwaysOff
                ? Color.FromArgb(255, 150, 156, 170)
                : Color.White;

        using (var bgBrush = new SolidBrush(bgColor))
        {
            g.FillEllipse(bgBrush, 2, 2, 28, 28);
        }

        // Draw crisp sun/backlight rays or crescent depending on mode
        using var pen = new Pen(fgColor, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var fgBrush = new SolidBrush(fgColor);

        if (mode == BacklightMode.AlwaysOff)
        {
            // Minimal horizontal bar / off dot
            g.DrawLine(pen, 10, 16, 22, 16);
        }
        else
        {
            // Center glow circle + 8 rays
            g.FillEllipse(fgBrush, 12, 12, 8, 8);
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4.0;
                float x1 = 16f + (float)(Math.Cos(angle) * 6.5);
                float y1 = 16f + (float)(Math.Sin(angle) * 6.5);
                float x2 = 16f + (float)(Math.Cos(angle) * 10.5);
                float y2 = 16f + (float)(Math.Sin(angle) * 10.5);
                g.DrawLine(pen, x1, y1, x2, y2);
            }
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
    }
}
