using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SettingsWindow : Window
{
    private static readonly SizeOption[] Sizes =
    [new(320, 180), new(640, 360), new(960, 540), new(1280, 720), new(1600, 900), new(1920, 1080)];
    private static readonly double[] Zooms = [1, 1.5, 2, 2.5, 3, 3.5, 4];
    private MagnifierSettings _settings;
    private readonly IScreenCapture _capture;
    private bool _syncing = true;
    private nint _windowHandle;
    private bool _captureExcluded;

    public SettingsWindow(MagnifierSettings settings, IScreenCapture capture, bool canResizeLens = false,
        string timingSummary = "")
    {
        _settings = settings;
        _capture = capture;
        InitializeComponent();
        ResizeLensButton.IsEnabled = canResizeLens;
        TimingSummaryText.Text = $"현재 {timingSummary}";
        DefaultSizeBox.ItemsSource = Sizes;
        DefaultZoomBox.ItemsSource = Zooms;
        VersionText.Text = $"버전 {GetDisplayVersion()}";
        RefreshControls();
        // Checked 이벤트는 모든 페이지의 XAML 필드가 연결된 뒤에 발생해야 한다.
        AppearancePageButton.IsChecked = true;
    }

    public event Action<MagnifierSettings>? SettingsChanged;
    public bool ResizeLensRequested { get; private set; }
    public bool InputTimingRequested { get; private set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowHandle = new WindowInteropHelper(this).Handle;
        SetCaptureExclusion(_settings.HideAppWindowsFromScreenCapture);
    }

    protected override void OnClosed(EventArgs e)
    {
        try { if (_captureExcluded) _capture.SetWindowCaptureExclusion(_windowHandle, false); }
        catch { }
        base.OnClosed(e);
    }

    private void RefreshControls()
    {
        _syncing = true;
        try
        {
            TopToolbarButton.IsChecked = _settings.ToolbarPlacement == ToolbarPlacement.Top;
            BottomToolbarButton.IsChecked = _settings.ToolbarPlacement == ToolbarPlacement.Bottom;
            ThemeBox.SelectedIndex = (int)_settings.Theme;
            LensDisplayModeBox.SelectedIndex = (int)_settings.LensDisplayMode;
            SourceIndicatorBox.SelectedIndex = (int)_settings.SourceIndicatorPreference;
            HideAppWindowsFromScreenCaptureBox.IsChecked = _settings.HideAppWindowsFromScreenCapture;
            RememberLayoutBox.IsChecked = _settings.RememberLayout;
            DefaultSizeBox.SelectedItem = Sizes.FirstOrDefault(x => x.Width == _settings.DefaultSourceWidth && x.Height == _settings.DefaultSourceHeight);
            DefaultZoomBox.SelectedItem = Zooms.OrderBy(x => Math.Abs(x - _settings.DefaultZoom)).First();
        }
        finally { _syncing = false; }
    }

    private static string GetDisplayVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
            return informationalVersion.Split('+', 2)[0];

        return assembly.GetName().Version?.ToString(3) ?? "개발 빌드";
    }

    private void ToolbarPlacement_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        Update(_settings with { ToolbarPlacement = BottomToolbarButton.IsChecked == true ? ToolbarPlacement.Bottom : ToolbarPlacement.Top });
    }

    private void ThemeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || ThemeBox.SelectedIndex < 0) return;
        Update(_settings with { Theme = (ThemePreference)ThemeBox.SelectedIndex });
    }

    private void LensDisplayMode_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || LensDisplayModeBox.SelectedIndex < 0) return;
        Update(_settings with { LensDisplayMode = (LensDisplayMode)LensDisplayModeBox.SelectedIndex });
    }

    private void SourceIndicator_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || SourceIndicatorBox.SelectedIndex < 0) return;
        Update(_settings with { SourceIndicatorPreference = (SourceIndicatorPreference)SourceIndicatorBox.SelectedIndex });
    }

    private void HideAppWindowsFromScreenCapture_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        Update(_settings with { HideAppWindowsFromScreenCapture = HideAppWindowsFromScreenCaptureBox.IsChecked == true });
    }

    private void ResizeLens_OnClick(object sender, RoutedEventArgs e)
    {
        ResizeLensRequested = true;
        Close();
    }

    private void RememberLayout_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        Update(_settings with { RememberLayout = RememberLayoutBox.IsChecked == true });
    }

    private void DefaultSizeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || DefaultSizeBox.SelectedItem is not SizeOption size) return;
        Update(_settings with { DefaultSourceWidth = size.Width, DefaultSourceHeight = size.Height });
    }

    private void DefaultZoomBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || DefaultZoomBox.SelectedItem is not double zoom) return;
        Update(_settings with { DefaultZoom = zoom });
    }

    private void Update(MagnifierSettings settings)
    {
        var captureExclusionChanged = _settings.HideAppWindowsFromScreenCapture != settings.HideAppWindowsFromScreenCapture;
        _settings = settings;
        if (captureExclusionChanged) SetCaptureExclusion(settings.HideAppWindowsFromScreenCapture);
        RefreshControls();
        SettingsChanged?.Invoke(_settings);
    }

    private void SetCaptureExclusion(bool excludeFromCapture)
    {
        if (_windowHandle == nint.Zero) return;
        _capture.SetWindowCaptureExclusion(_windowHandle, excludeFromCapture);
        _captureExcluded = excludeFromCapture;
    }

    private void AppearancePage_OnClick(object sender, RoutedEventArgs e) => ShowPage(AppearancePanel);
    private void DefaultsPage_OnClick(object sender, RoutedEventArgs e) => ShowPage(DefaultsPanel);
    private void AdvancedPage_OnClick(object sender, RoutedEventArgs e) => ShowPage(AdvancedPanel);
    private void AboutPage_OnClick(object sender, RoutedEventArgs e) => ShowPage(AboutPanel);

    // The timing panel is non-modal (compared live in the target app), so this modal closes first.
    private void InputTiming_OnClick(object sender, RoutedEventArgs e)
    {
        InputTimingRequested = true;
        Close();
    }

    private void ShowPage(FrameworkElement page)
    {
        AppearancePanel.Visibility = page == AppearancePanel ? Visibility.Visible : Visibility.Collapsed;
        DefaultsPanel.Visibility = page == DefaultsPanel ? Visibility.Visible : Visibility.Collapsed;
        AdvancedPanel.Visibility = page == AdvancedPanel ? Visibility.Visible : Visibility.Collapsed;
        AboutPanel.Visibility = page == AboutPanel ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenDiagnostics_OnClick(object sender, RoutedEventArgs e)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Magnifier", "diagnostics");
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    private void SettingsWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Close();
        e.Handled = true;
    }

    private sealed record SizeOption(int Width, int Height)
    {
        public override string ToString() => $"{Width} × {Height}";
    }
}
