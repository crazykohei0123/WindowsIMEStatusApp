using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using ImeStatusOverlay.Detection;
using ImeStatusOverlay.Recognition;
using ImeStatusOverlay.Startup;
using ImeStatusOverlay.Storage;
using ImeStatusOverlay.UI;
using WinForms = System.Windows.Forms;

namespace ImeStatusOverlay;

public partial class App : System.Windows.Application
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan RelocateInterval = TimeSpan.FromSeconds(2);

    private Classifier _classifier = null!;
    private AppSettings _settings = null!;
    private ImeStateTracker _tracker = null!;
    private OverlayWindow _overlay = null!;
    private WinForms.NotifyIcon _tray = null!;
    private DispatcherTimer _timer = null!;

    private Rectangle? _region;
    private DateTime _regionLocatedAt = DateTime.MinValue;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _classifier = new Classifier();
        _settings = new AppSettings();
        _tracker = new ImeStateTracker();
        _overlay = new OverlayWindow();
        SetupTray();

        if (!_classifier.IsCalibrated)
            RunCalibration();

        StartPolling();
    }

    private void SetupTray()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("今の状態を表示", null, (_, _) => ShowCurrentOnce());
        menu.Items.Add("設定...", null, (_, _) => ShowSettings());
        menu.Items.Add("再キャリブレーション", null, (_, _) => RunCalibration());

        var startupItem = new WinForms.ToolStripMenuItem("スタートアップに登録")
        {
            CheckOnClick = true,
            Checked = StartupRegistration.IsEnabled(),
        };
        startupItem.CheckedChanged += (_, _) => StartupRegistration.SetEnabled(startupItem.Checked);
        menu.Items.Add(startupItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => Shutdown());

        _tray = new WinForms.NotifyIcon
        {
            Text = "IME状態表示",
            Icon = LoadAppIcon(),
            Visible = true,
            ContextMenuStrip = menu,
        };
    }

    private void RunCalibration()
    {
        _timer?.Stop();
        var win = new CalibrationWindow(_classifier);
        win.ShowDialog();
        // Reset detection so the freshly calibrated state is picked up cleanly.
        _tracker.Reset();
        _region = null;
        _timer?.Start();
    }

    private void StartPolling()
    {
        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    private void Poll()
    {
        try
        {
            if (!_classifier.IsCalibrated)
                return;

            // Re-locate the indicator region periodically (taskbar layout can shift).
            if (_region == null || DateTime.UtcNow - _regionLocatedAt > RelocateInterval)
            {
                _region = Indicator.TryFindIndicatorRect();
                _regionLocatedAt = DateTime.UtcNow;
                if (_region == null)
                    return;
            }

            var data = Indicator.CaptureGray(_region.Value, out int w, out int h);
            var state = _classifier.Classify(data, w, h);

            if (_tracker.OnSample(state, out ImeState committed))
                _overlay.ShowState(committed, _settings.OnText, _settings.OffText);
        }
        catch
        {
            // Never let polling crash the tray app.
        }
    }

    private void ShowCurrentOnce()
    {
        if (_tracker.Committed != ImeState.Unknown)
            _overlay.ShowState(_tracker.Committed, _settings.OnText, _settings.OffText);
    }

    private void ShowSettings()
    {
        var win = new SettingsWindow(_settings);
        win.ShowDialog();
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        // Prefer the bundled ICO next to the executable; fall back to a system icon.
        try
        {
            var dir = System.AppContext.BaseDirectory;
            var path = System.IO.Path.Combine(dir, "app.ico");
            return System.IO.File.Exists(path)
                ? new System.Drawing.Icon(path)
                : SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _timer?.Stop();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        base.OnExit(e);
    }
}
