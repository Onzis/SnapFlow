using System;
using System.Windows;
using System.Windows.Forms;
using WpfApp = System.Windows.Application;

namespace Screenshoter;

public partial class App : WpfApp
{
    private NotifyIcon? _tray;
    private HotkeyManager? _hotkey;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settings;

    private AppSettings _cfg = new();
    private ToolStripMenuItem? _captureItem;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _cfg = AppSettings.Load();
        InitTray();
        RegisterHotkey();
    }

    private void InitTray()
    {
        var menu = new ContextMenuStrip();
        _captureItem = new ToolStripMenuItem(CaptureMenuText(), null, (_, __) => StartCapture());
        menu.Items.Add(_captureItem);
        menu.Items.Add("Настройки…", null, (_, __) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, __) => ExitApp());

        _tray = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "Screenshoter",
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, __) => StartCapture();
    }

    private string CaptureMenuText() => $"Скриншот области ({_cfg.ToDisplayString()})";

    private void RegisterHotkey()
    {
        _hotkey?.Dispose();
        _hotkey = null;
        try
        {
            _hotkey = new HotkeyManager(_cfg.ToWin32Mod(), _cfg.ToVirtualKey());
            _hotkey.HotkeyPressed += StartCapture;
        }
        catch (Exception ex)
        {
            _tray?.ShowBalloonTip(4000, "Screenshoter",
                $"Горячая клавиша {_cfg.ToDisplayString()} недоступна. Используйте меню в трее.\n{ex.Message}",
                ToolTipIcon.Warning);
        }
    }

    private void OpenSettings()
    {
        if (_settings != null) { _settings.Activate(); return; }

        _settings = new SettingsWindow(_cfg);
        var ok = _settings.ShowDialog() == true;
        var result = _settings;
        _settings = null;

        if (!ok) return;

        _cfg.Modifiers = result.SelectedModifiers;
        _cfg.Key = result.SelectedKey;
        _cfg.Save();

        if (_captureItem != null) _captureItem.Text = CaptureMenuText();
        RegisterHotkey();
    }

    private void StartCapture()
    {
        if (_overlay != null && _overlay.IsVisible) return;

        var shot = ScreenshotHelper.CaptureVirtualScreen(
            out double left, out double top, out double width, out double height);

        _overlay = new OverlayWindow(shot, left, top, width, height);
        _overlay.Closed += (_, __) => _overlay = null;
        _overlay.Show();
        _overlay.Activate();
    }

    private void ExitApp()
    {
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _hotkey?.Dispose();
        Shutdown();
    }
}
