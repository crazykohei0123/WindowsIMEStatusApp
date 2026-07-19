using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace ImeStatusOverlay;

public partial class App : System.Windows.Application
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan RelocateInterval = TimeSpan.FromSeconds(2);
    private const int StableCountRequired = 2; // consecutive reads to commit a change

    private Classifier _classifier = null!;
    private OverlayWindow _overlay = null!;
    private WinForms.NotifyIcon _tray = null!;
    private DispatcherTimer _timer = null!;

    private Rectangle? _region;
    private DateTime _regionLocatedAt = DateTime.MinValue;

    private ImeState _committed = ImeState.Unknown;
    private ImeState _candidate = ImeState.Unknown;
    private int _candidateCount;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _classifier = new Classifier();
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
            Icon = SystemIcons.Application,
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
        _committed = ImeState.Unknown;
        _candidate = ImeState.Unknown;
        _candidateCount = 0;
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

            if (state == ImeState.Unknown)
            {
                // Ambiguous or size mismatch: do not change the committed state.
                _candidate = ImeState.Unknown;
                _candidateCount = 0;
                return;
            }

            if (state == _committed)
            {
                _candidate = ImeState.Unknown;
                _candidateCount = 0;
                return;
            }

            // Debounce: require the same new state a few reads in a row.
            if (state == _candidate)
                _candidateCount++;
            else
            {
                _candidate = state;
                _candidateCount = 1;
            }

            if (_candidateCount >= StableCountRequired)
            {
                _committed = state;
                _candidate = ImeState.Unknown;
                _candidateCount = 0;
                _overlay.ShowState(state);
            }
        }
        catch
        {
            // Never let polling crash the tray app.
        }
    }

    private void ShowCurrentOnce()
    {
        if (_committed != ImeState.Unknown)
            _overlay.ShowState(_committed);
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