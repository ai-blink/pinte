using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Magnifier.Core;

namespace Magnifier.App;

// Trial panel for relay output timing. Edits stay local until Apply; the relay worker
// swaps the whole snapshot at its next idle boundary and reports the applied revision.
public partial class PointerTimingWindow : Window
{
    private sealed record Field(string Label, string Tip, int Max,
        Func<PointerTimingSettings, int> Get, Func<PointerTimingSettings, int, PointerTimingSettings> Set);

    private static readonly Field[] Fields =
    [
        new("도착 후 누르기 대기", "커서를 원본 위치로 옮긴 뒤 누름(Down)을 보내기까지 최소 대기",
            PointerTimingSettings.MaximumArrivalMs, s => s.ArrivalMs, (s, v) => s with { ArrivalMs = v }),
        new("최소 누름 유지", "누름(Down) 뒤 해제(Up)를 보낼 수 있는 가장 이른 시점",
            PointerTimingSettings.MaximumHoldMs, s => s.MinimumHoldMs, (s, v) => s with { MinimumHoldMs = v }),
        new("해제 후 체류", "해제(Up) 뒤 커서를 렌즈 위치로 되돌리기까지 원본 위치 유지",
            PointerTimingSettings.MaximumPostReleaseMs, s => s.PostReleaseMs, (s, v) => s with { PostReleaseMs = v }),
        new("제스처 간격", "커서 복귀 뒤 줄 서 있던 다음 누름을 시작하기까지 대기",
            PointerTimingSettings.MaximumBetweenGesturesMs, s => s.BetweenGesturesMs, (s, v) => s with { BetweenGesturesMs = v })
    ];

    private readonly ILivePointerRelay _relay;
    private readonly TextBox[] _inputs = new TextBox[Fields.Length];
    private readonly TextBlock[] _appliedLabels = new TextBlock[Fields.Length];
    private PointerTimingSettings _edit;
    private bool _applying;

    internal PointerTimingWindow(ILivePointerRelay relay, PointerTimingProfile profile, string? loadError)
    {
        InitializeComponent();
        _relay = relay;
        Profile = profile;
        _edit = profile.Applied;
        BuildRows();
        foreach (var (name, value) in PointerTimingSettings.Presets)
        {
            var button = new Button { Style = (Style)FindResource("AppButtonStyle"), Height = 32, Margin = new(2, 0, 2, 0),
                Content = name, ToolTip = value.ToString() };
            button.Click += (_, _) => SetEdit(value);
            PresetButtons.Children.Add(button);
        }
        _relay.TimingChanged += OnTimingChanged;
        Closed += (_, _) => _relay.TimingChanged -= OnTimingChanged;
        Render(loadError);
    }

    internal PointerTimingProfile Profile { get; private set; }
    internal event Action<PointerTimingProfile>? ProfileChanged;

    private void BuildRows()
    {
        for (var i = 0; i < Fields.Length; i++)
        {
            var field = Fields[i];
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
        else Render("숫자(ms)만 입력할 수 있습니다.");
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
        for (var i = 0; i < Fields.Length; i++)
        {
            if (!_inputs[i].IsKeyboardFocused) _inputs[i].Text = Fields[i].Get(_edit).ToString();
            _appliedLabels[i].Text = $"({Fields[i].Get(status.Applied)}ms)";
        }
        ApplyAButton.Content = "A 적용";
        ApplyAButton.ToolTip = Profile.SlotA.ToString();
        ApplyBButton.Content = "B 적용";
        ApplyBButton.ToolTip = Profile.SlotB.ToString();
        PreviousButton.IsEnabled = Profile.Previous is not null && !_applying;
        ApplyButton.IsEnabled = !_applying;
        var dirty = _edit != status.Applied || status.Pending is not null;
        var state = status.Pending is { } pending
            ? $"적용 대기 rev{status.PendingRevision} {pending} · 진행 중 입력이 끝나면 바뀝니다"
            : $"적용됨 rev{status.AppliedRevision} {status.Applied}";
        var slot = status.Applied == Profile.SlotA ? " · A" : status.Applied == Profile.SlotB ? " · B" : "";
        StatusText.Text = (message is null ? "" : message + "\n") + state + slot
            + (dirty && status.Pending is null ? "\n편집값이 아직 적용되지 않았습니다." : "")
            + $"\nA {Profile.SlotA} · B {Profile.SlotB}";
    }

    private async Task ApplyAsync(PointerTimingSettings value)
    {
        if (_applying) return;
        if (!value.IsValid()) { Render("허용 범위를 벗어난 값입니다."); return; }
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
            if (!PointerTimingStore.Save(next)) message = "프로필 저장 실패 · 이번 실행에만 적용합니다.";
            ProfileChanged?.Invoke(next);
        }
        catch (Exception exception)
        {
            message = $"적용 실패 · 기존값 유지: {exception.Message}";
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
        Render(PointerTimingStore.Save(next) ? message : message + " · 프로필 저장 실패");
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
    private void SaveA_OnClick(object sender, RoutedEventArgs e) => UpdateSlots(Profile with { SlotA = _edit }, $"A에 저장 {_edit}");
    private void SaveB_OnClick(object sender, RoutedEventArgs e) => UpdateSlots(Profile with { SlotB = _edit }, $"B에 저장 {_edit}");
}
