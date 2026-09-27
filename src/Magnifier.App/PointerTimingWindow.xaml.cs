using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Magnifier.App.Localization;
using Magnifier.Core;

namespace Magnifier.App;

// Trial panel for relay output timing. Edits stay local until Apply; the relay worker
// swaps the whole snapshot at its next idle boundary and reports the applied revision.
public partial class PointerTimingWindow : Window
{
    private sealed record Field(string Label, string Tip, int Max,
        Func<PointerTimingSettings, int> Get, Func<PointerTimingSettings, int, PointerTimingSettings> Set);

    // Built per-instance (not static readonly) so the current language applies — a static
    // array would freeze these labels to whatever language was current at type load.
    private static Field[] BuildFields() =>
    [
        new(Loc.Instance["Timing_Field_Arrival_Label"], Loc.Instance["Timing_Field_Arrival_Tip"],
            PointerTimingSettings.MaximumArrivalMs, s => s.ArrivalMs, (s, v) => s with { ArrivalMs = v }),
        new(Loc.Instance["Timing_Field_Hold_Label"], Loc.Instance["Timing_Field_Hold_Tip"],
            PointerTimingSettings.MaximumHoldMs, s => s.MinimumHoldMs, (s, v) => s with { MinimumHoldMs = v }),
        new(Loc.Instance["Timing_Field_PostRelease_Label"], Loc.Instance["Timing_Field_PostRelease_Tip"],
            PointerTimingSettings.MaximumPostReleaseMs, s => s.PostReleaseMs, (s, v) => s with { PostReleaseMs = v }),
        new(Loc.Instance["Timing_Field_Gesture_Label"], Loc.Instance["Timing_Field_Gesture_Tip"],
            PointerTimingSettings.MaximumBetweenGesturesMs, s => s.BetweenGesturesMs, (s, v) => s with { BetweenGesturesMs = v })
    ];

    private readonly ILivePointerRelay _relay;
    private readonly Field[] _fields = BuildFields();
    private readonly TextBox[] _inputs;
    private readonly TextBlock[] _appliedLabels;
    private PointerTimingSettings _edit;
    private bool _applying;

    internal PointerTimingWindow(ILivePointerRelay relay, PointerTimingProfile profile, string? loadError)
    {
        InitializeComponent();
        _relay = relay;
        _inputs = new TextBox[_fields.Length];
        _appliedLabels = new TextBlock[_fields.Length];
        Profile = profile;
        _edit = profile.Applied;
        BuildRows();
        foreach (var (name, value) in PointerTimingSettings.Presets)
        {
            var button = new Button { Style = (Style)FindResource("AppButtonStyle"), Height = 32, Margin = new(2, 0, 2, 0),
                Content = Loc.Instance[$"Timing_Preset_{name}"], ToolTip = value.ToString() };
            button.Click += (_, _) => SetEdit(value);
            PresetButtons.Children.Add(button);
        }
        _relay.TimingChanged += OnTimingChanged;
        Loc.Instance.PropertyChanged += OnLanguageChanged;
        Closed += (_, _) =>
        {
            _relay.TimingChanged -= OnTimingChanged;
            Loc.Instance.PropertyChanged -= OnLanguageChanged;
        };
        Render(loadError);
    }

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Render();

    internal PointerTimingProfile Profile { get; private set; }
    internal event Action<PointerTimingProfile>? ProfileChanged;

    private void BuildRows()
    {
        for (var i = 0; i < _fields.Length; i++)
        {
            var field = _fields[i];
            var row = new Grid { Margin = new(0, 3, 0, 3), ToolTip = field.Tip };
            foreach (var width in new[] { 128.0, 34, 58, 34, 1 })
                row.ColumnDefinitions.Add(new() { Width = width == 1 ? new GridLength(1, GridUnitType.Star) : new GridLength(width) });
            var label = new TextBlock { Text = field.Label, VerticalAlignment = VerticalAlignment.Center };
            var minus = StepButton("−", field, -1);
            var input = new TextBox { Height = 30, Margin = new(3, 0, 3, 0), TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center };
            input.LostFocus += (_, _) => CommitInput(field, input);
            input.KeyDown += (_, e) => { if (e.Key == Key.Enter) CommitInput(field, input); };
            var plus = StepButton("+", field, 1);
            var applied = new TextBlock { Margin = new(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("AppMutedTextBrush"), FontSize = 11 };
            Grid.SetColumn(minus, 1);
            Grid.SetColumn(input, 2);
            Grid.SetColumn(plus, 3);
            Grid.SetColumn(applied, 4);
            row.Children.Add(label);
            row.Children.Add(minus);
            row.Children.Add(input);
            row.Children.Add(plus);
            row.Children.Add(applied);
            _inputs[i] = input;
            _appliedLabels[i] = applied;
            Rows.Children.Add(row);
        }
    }

    private Button StepButton(string text, Field field, int direction)
    {
        var button = new Button { Style = (Style)FindResource("AppButtonStyle"), Height = 30, Content = text, FontSize = 15 };
        button.Click += (_, _) =>
        {
            var value = field.Get(_edit);
            SetEdit(field.Set(_edit, Math.Clamp(value + PointerTimingSettings.StepFrom(value, direction), 0, field.Max)));
        };
        return button;
    }

    private void CommitInput(Field field, TextBox input)
    {
        if (int.TryParse(input.Text.Trim().TrimEnd('m', 's'), out var value))
            SetEdit(field.Set(_edit, Math.Clamp(value, 0, field.Max)));
        else Render(Loc.Instance["Timing_Error_NotANumber"]);
    }

    private void SetEdit(PointerTimingSettings value)
    {
        _edit = value;
        Render();
    }

    private void OnTimingChanged(PointerTimingStatus status) => Dispatcher.BeginInvoke(() => Render());

    private void Render(string? message = null)
    {
        var status = _relay.TimingStatus;
        for (var i = 0; i < _fields.Length; i++)
        {
            if (!_inputs[i].IsKeyboardFocused) _inputs[i].Text = _fields[i].Get(_edit).ToString();
            _appliedLabels[i].Text = $"({_fields[i].Get(status.Applied)}ms)";
        }
        ApplyAButton.Content = Loc.Instance["Timing_ApplyA_Content"];
        ApplyAButton.ToolTip = Profile.SlotA.ToString();
        ApplyBButton.Content = Loc.Instance["Timing_ApplyB_Content"];
        ApplyBButton.ToolTip = Profile.SlotB.ToString();
        PreviousButton.IsEnabled = Profile.Previous is not null && !_applying;
        ApplyButton.IsEnabled = !_applying;
        var dirty = _edit != status.Applied || status.Pending is not null;
        var state = status.Pending is { } pending
            ? string.Format(Loc.Instance["Timing_Status_Pending_Format"], status.PendingRevision, pending)
            : string.Format(Loc.Instance["Timing_Status_Applied_Format"], status.AppliedRevision, status.Applied);
        var slot = status.Applied == Profile.SlotA ? Loc.Instance["Timing_Status_SlotA_Suffix"]
            : status.Applied == Profile.SlotB ? Loc.Instance["Timing_Status_SlotB_Suffix"] : "";
        StatusText.Text = (message is null ? "" : message + "\n") + state + slot
            + (dirty && status.Pending is null ? Loc.Instance["Timing_Status_DirtyNote"] : "")
            + string.Format(Loc.Instance["Timing_Status_SlotsSummary_Format"], Profile.SlotA, Profile.SlotB);
    }

    private async Task ApplyAsync(PointerTimingSettings value)
    {
        if (_applying) return;
        if (!value.IsValid()) { Render(Loc.Instance["Timing_Error_OutOfRange"]); return; }
        _applying = true;
        var next = Profile with
        {
            Revision = Profile.Revision + 1,
            Applied = value,
            Previous = value == Profile.Applied ? Profile.Previous : Profile.Applied
        };
        string? message = null;
        try
        {
            await _relay.ApplyTimingAsync(value, next.Revision);
            Profile = next;
            _edit = value;
            if (!PointerTimingStore.Save(next)) message = Loc.Instance["Timing_Error_SaveFailed_SessionOnly"];
            ProfileChanged?.Invoke(next);
        }
        catch (Exception exception)
        {
            message = string.Format(Loc.Instance["Timing_Error_ApplyFailed_Format"], exception.Message);
        }
        finally
        {
            _applying = false;
            Render(message);
        }
    }

    private void UpdateSlots(PointerTimingProfile next, string message)
    {
        Profile = next;
        Render(PointerTimingStore.Save(next) ? message : message + Loc.Instance["Timing_Error_SaveFailed_Suffix"]);
        ProfileChanged?.Invoke(next);
    }

    private async void Apply_OnClick(object sender, RoutedEventArgs e) => await ApplyAsync(_edit);
    private async void ApplyA_OnClick(object sender, RoutedEventArgs e) => await ApplyAsync(Profile.SlotA);
    private async void ApplyB_OnClick(object sender, RoutedEventArgs e) => await ApplyAsync(Profile.SlotB);
    private async void Default_OnClick(object sender, RoutedEventArgs e) => await ApplyAsync(PointerTimingSettings.Default);
    private async void Previous_OnClick(object sender, RoutedEventArgs e)
    {
        if (Profile.Previous is { } previous) await ApplyAsync(previous);
    }
    private void SaveA_OnClick(object sender, RoutedEventArgs e) =>
        UpdateSlots(Profile with { SlotA = _edit }, string.Format(Loc.Instance["Timing_SaveA_Message_Format"], _edit));
    private void SaveB_OnClick(object sender, RoutedEventArgs e) =>
        UpdateSlots(Profile with { SlotB = _edit }, string.Format(Loc.Instance["Timing_SaveB_Message_Format"], _edit));
}
