using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ImeStatusOverlay;

public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _holdTimer;
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan FadeInDuration = TimeSpan.FromMilliseconds(110);
    private static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(260);

    public OverlayWindow()
    {
        InitializeComponent();

        _holdTimer = new DispatcherTimer { Interval = HoldDuration };
        _holdTimer.Tick += (_, _) =>
        {
            _holdTimer.Stop();
            BeginFade(0.0, FadeOutDuration, () => Hide());
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        MakeClickThrough();
    }

    /// <summary>Shows the big state text centered on the primary monitor.</summary>
    public void ShowState(ImeState state)
    {
        StateText.Text = state == ImeState.On ? "IME ON" : "IME OFF";
        StateText.Foreground = new SolidColorBrush(
            state == ImeState.On
                ? Color.FromRgb(0x4A, 0xDE, 0x80)   // green
                : Color.FromRgb(0xE5, 0xE7, 0xEB)); // light gray

        PositionCenter();

        _holdTimer.Stop();
        Show();
        BeginAnimation(OpacityProperty, null); // cancel any running fade
        BeginFade(1.0, FadeInDuration, () => _holdTimer.Start());
    }

    private void BeginFade(double to, TimeSpan duration, Action onDone)
    {
        var anim = new DoubleAnimation(to, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        anim.Completed += (_, _) => onDone();
        BeginAnimation(OpacityProperty, anim);
    }

    private void PositionCenter()
    {
        // SystemParameters.WorkArea is in WPF DIPs; no DPI conversion needed.
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = DesiredSize;
        var wa = SystemParameters.WorkArea;
        Left = wa.Left + (wa.Width - size.Width) / 2.0;
        Top = wa.Top + (wa.Height - size.Height) / 2.0 - 40;
    }

    // ------------------------------------------------------------------
    // Click-through / non-activating layered window
    // ------------------------------------------------------------------

    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, ex);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}