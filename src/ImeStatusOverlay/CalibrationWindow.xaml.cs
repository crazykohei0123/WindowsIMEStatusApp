using System.Drawing;
using System.Windows;
using System.Windows.Threading;

namespace ImeStatusOverlay;

public partial class CalibrationWindow : Window
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(200);
    private const int TotalSamples = 40; // 40 * 200ms = 8 seconds

    private readonly Classifier _classifier;
    private DispatcherTimer? _timer;
    private Rectangle _region;
    private int _tick;

    // signature -> (count, gray, ink)
    private readonly Dictionary<string, (int Count, byte[] Gray, int Ink)> _samples = new();
    private int _w;
    private int _h;

    public CalibrationWindow(Classifier classifier)
    {
        InitializeComponent();
        _classifier = classifier;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        var rect = Indicator.TryFindIndicatorRect();
        if (rect == null)
        {
            Status.Text = "インジケーターが見つかりません。タスクバーにIMEモード表示があるか確認してください。";
            return;
        }

        _region = rect.Value;
        _samples.Clear();
        _tick = 0;
        StartButton.IsEnabled = false;
        DoneButton.IsEnabled = false;
        PreviewPanel.Visibility = Visibility.Collapsed;

        _timer = new DispatcherTimer { Interval = SampleInterval };
        _timer.Tick += SampleTick;
        _timer.Start();
        Status.Text = "切り替えてください... 0 / 8 秒";
    }

    private void SampleTick(object? sender, EventArgs e)
    {
        _tick++;
        var gray = Indicator.CaptureGray(_region, out _w, out _h);
        var sig = Glyph.Signature(gray, _w, _h, out int ink);

        if (_samples.TryGetValue(sig, out var entry))
            _samples[sig] = (entry.Count + 1, entry.Gray, entry.Ink);
        else
            _samples[sig] = (1, gray, ink);

        int seconds = _tick * (int)SampleInterval.TotalMilliseconds / 1000;
        Status.Text = $"切り替えてください... {seconds} / 8 秒  (検出パターン数: {_samples.Count})";

        if (_tick >= TotalSamples)
        {
            _timer!.Stop();
            FinalizeSampling();
        }
    }

    private void FinalizeSampling()
    {
        var top = _samples
            .OrderByDescending(kv => kv.Value.Count)
            .Take(2)
            .ToList();

        if (top.Count < 2)
        {
            Status.Text = "変化が検出されませんでした。切り替えがタスクバーに反映されるか確認し、もう一度「開始」を押してください。";
            StartButton.IsEnabled = true;
            return;
        }

        // あ (ON) has more ink than A (OFF).
        var ordered = top.OrderByDescending(kv => kv.Value.Ink).ToList();
        var on = ordered[0];   // more ink
        var off = ordered[1];  // less ink

        _classifier.SetTemplates(off.Value.Gray, on.Value.Gray, _w, _h);

        OffPreview.Text = Render(off.Key);
        OnPreview.Text = Render(on.Key);
        PreviewPanel.Visibility = Visibility.Visible;

        Status.Text = $"学習しました (OFF ink={off.Value.Ink}, ON ink={on.Value.Ink})。問題なければ完了してください。";
        DoneButton.IsEnabled = true;
        StartButton.IsEnabled = true;
    }

    private string Render(string signature)
    {
        var lines = new string[_h];
        for (int y = 0; y < _h; y++)
            lines[y] = signature.Substring(y * _w, _w);
        return string.Join("\n", lines);
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}