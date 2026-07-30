using System.Windows;
using ImeStatusOverlay.Storage;

namespace ImeStatusOverlay.UI;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        OnTextBox.Text = _settings.OnText;
        OffTextBox.Text = _settings.OffText;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.SetTexts(OnTextBox.Text, OffTextBox.Text);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Defaults_Click(object sender, RoutedEventArgs e)
    {
        OnTextBox.Text = AppSettings.DefaultOnText;
        OffTextBox.Text = AppSettings.DefaultOffText;
        Status.Text = $"既定値: ON=\"{AppSettings.DefaultOnText}\", OFF=\"{AppSettings.DefaultOffText}\"";
    }
}
