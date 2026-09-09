using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Magnifier.Core;
using Magnifier.Infrastructure;

internal static class RelayProbe
{
    private const nint ProbeTag = 0x50524F42;
    private const nint RelayTag = 0x4D41474E;
    private static readonly List<Receipt> Receipts = [];
    private static readonly List<object> Results = [];
    private static readonly List<object> Conflicts = [];
    private static readonly HookProc GuardProc = Guard;
    private static WindowsLivePointerRelay? _relay;
    private static RelayStatus? _status;
    private static nint _targetHwnd, _lensHwnd, _guard;
    private static bool _conflict, _buttonSent, _targetPressed;
    private static int _lensDowns;
    private static ScreenPoint _targetOrigin;
    private static Window? _target, _lens;
    private static ScreenPoint _originalCursor;
    private static object? _lastCommand;
    private static long _commandNumber;
    private static ScreenPoint? _lastProbePoint;
    private static LensViewport? _currentViewport;

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--describe"))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { EngineBuild = typeof(WindowsLivePointerRelay).Assembly.ManifestModule.ModuleVersionId, InputAbi = DescribeAbi() }));
            return 0;
        }
        SetProcessDpiAwarenessContext(-4);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => await Run(app);
        app.Run();
        return Environment.ExitCode;
    }

    private static async Task Run(Application app)
    {
        DispatcherTimer? frameTimer = null;
        try
        {
            GetCursorPos(out var cursor);
            _originalCursor = new(cursor.X, cursor.Y);
            Check(!ButtonsDown(), "실행 전에 물리 버튼이 눌려 있음");
            var area = SystemParameters.WorkArea;
            Check(area.Width >= 1100 && area.Height >= 650, "검증 창 두 개를 배치할 화면 공간 부족");
            _target = MakeWindow("Magnifier 전달 검증 대상", area.Left + 50, area.Top + 70, 320, 280, false);
            _lens = MakeWindow("Magnifier 전달 검증 렌즈", area.Left + 470, area.Top + 70, 520, 440, true);
            _target.Show();
            _lens.Show();
            _targetHwnd = new WindowInteropHelper(_target).Handle;
            _lensHwnd = new WindowInteropHelper(_lens).Handle;
            HwndSource.FromHwnd(_targetHwnd)!.AddHook(TargetMessages);
            HwndSource.FromHwnd(_lensHwnd)!.AddHook(LensMessages);
            _target.MouseDown += (_, args) => { if (args.ChangedButton == MouseButton.Left) _target.CaptureMouse(); };
            _target.MouseUp += (_, args) => { if (args.ChangedButton == MouseButton.Left) _target.ReleaseMouseCapture(); };
            _target.Activate();
            await Task.Delay(180);
            CheckOwnedForeground();
            _relay = new WindowsLivePointerRelay();
            _relay.StatusChanged += state => Volatile.Write(ref _status, state);
            await _relay.StopAsync("probe 초기화");
            // 중계 후 설치하여 사용자 입력을 먼저 관측한다. 감지 뒤 추가 입력은 보내지 않는다.
            _guard = SetWindowsHookEx(14, GuardProc, GetModuleHandle(null), 0);
            Check(_guard != 0, "공유 데스크톱 충돌 감시 설치 실패");
            frameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            frameTimer.Tick += (_, _) => _relay.RefreshFrame();
            frameTimer.Start();

            await DragScenario("분리 창 곡선·왕복", overlap: false);
            await DragScenario("겹친 창 곡선·왕복", overlap: true);
            await BoundaryScenario();
            Console.WriteLine(JsonSerializer.Serialize(new { Result = "PASS", EngineBuild = typeof(WindowsLivePointerRelay).Assembly.ManifestModule.ModuleVersionId, Scenarios = Results }));
        }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            if (_relay is not null)
            {
                try { await _relay.StopAsync("probe 판정 전 해제"); await Task.Delay(120); }
                catch { }
            }
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Result = _conflict ? "NEEDS_USER_UI_CHECK" : "FAIL",
                EngineBuild = typeof(WindowsLivePointerRelay).Assembly.ManifestModule.ModuleVersionId,
                InputAbi = DescribeAbi(), Viewport = _currentViewport,
                Error = ex.ToString(), Scenarios = Results, Status = _status, Conflicts, Receipts
            }));
        }
        finally
        {
            frameTimer?.Stop();
            if (_relay is not null)
            {
                try { await _relay.StopAsync("probe 종료"); await _relay.DisposeAsync(); }
                catch (Exception ex) { Console.WriteLine("종료 해제 실패: " + ex.Message); Environment.ExitCode = 1; }
            }
            // 자체 검증 입력의 Up만 정리한다. 사용자 입력 충돌 뒤에는 입력/포커스를 조작하지 않는다.
            if (_buttonSent && !_conflict && IsOwnedForeground()) Send(4, null, RelayTag);
            if (_guard != 0) UnhookWindowsHookEx(_guard);
            _lens?.Close();
            _target?.Close();
            if (!_conflict) Send(0xC001, _originalCursor, ProbeTag);
            app.Shutdown();
        }
    }

    private static async Task DragScenario(string name, bool overlap)
    {
        Check(_relay is not null && _lens is not null && _target is not null, "검증 초기화 실패");
        await _relay!.StopAsync("다음 probe");
        if (overlap)
        {
            _lens!.Left = _target!.Left - 10;
            _lens.Top = _target.Top - 10;
            await Task.Delay(120);
        }
        var view = Viewport();
        await _relay.ConfigureAsync(view, _lensHwnd);
        _relay.RefreshFrame();
        Check(await _relay.StartAsync(), "조작 시작 거부: " + _status?.Message);
        Receipts.Clear();
        _lensDowns = 0;
        var path = new[] { (0.20, 0.20), (0.35, 0.25), (0.50, 0.45), (0.65, 0.65), (0.50, 0.45), (0.35, 0.25), (0.20, 0.20) };
        var points = path.Select(p => DestinationPoint(view, p.Item1, p.Item2)).ToArray();
        await Move(points[0]);
        Check(_status?.IsRelaying == true, "렌즈 진입이 중계로 전환되지 않음: " + _status?.Message);
        Check(((long)GetWindowLongPtr(_lensHwnd, -20) & 0x20) != 0, "렌즈가 입력 통과 스타일로 전환되지 않음");
        await Button(down: true);
        foreach (var point in points.Skip(1)) await Move(point);
        await Button(down: false);
        var expected = points.Select(p => view.MapToSource(new PreviewPoint(p.X, p.Y))).ToArray();
        ValidateDrag(expected);
        Check(_status?.IsPressed == false, "완료 뒤 중계 누름이 남음");
        Check(_lensDowns == 0, "렌즈가 대상 Down을 수신함");
        Results.Add(new { Name = name, Result = "PASS", Expected = expected, Receipts = Receipts.ToArray(), LensDowns = _lensDowns });
        await _relay.StopAsync("probe 시나리오 완료");
        Check(((long)GetWindowLongPtr(_lensHwnd, -20) & 0x20) == 0, "중지 뒤 렌즈 스타일 미복구");
    }

    private static async Task BoundaryScenario()
    {
        var view = Viewport();
        await _relay!.ConfigureAsync(view, _lensHwnd);
        _relay.RefreshFrame();
        Check(await _relay.StartAsync(), "경계 probe 시작 실패");
        Receipts.Clear();
        _lensDowns = 0;
        var start = DestinationPoint(view, .25, .35);
        var last = DestinationPoint(view, .75, .55);
        var boundary = new ScreenPoint(view.Destination.X + view.Destination.Width + 8, last.Y);
        await Move(start);
        await Button(down: true);
        await Move(last);
        await Move(boundary);
        Check(_status is { IsEnabled: false, IsPressed: false, WaitingForRelease: true }, "경계 중지·해제 대기 상태 불일치");
        Check(!await _relay.StartAsync(), "물리 버튼을 놓기 전에 재무장됨");
        Check(Receipts.Count(r => r.Kind == "down") == 1 && Receipts.Count(r => r.Kind == "up") == 1, "경계에서 정확히 한 번 해제되지 않음");
        var lastTarget = view.MapToSource(new PreviewPoint(last.X, last.Y));
        Check(Near(Receipts.Last(r => r.Kind == "up").Point, lastTarget), "경계 Up이 마지막 유효 지점과 다름");
        var startTarget = view.MapToSource(new PreviewPoint(start.X, start.Y));
        Check(Receipts.Where(r => r.Kind == "move" && r.Pressed).All(r => Near(r.Point, lastTarget) || Near(r.Point, startTarget)), "경계 복귀로 긴 획이 전달됨");
        await Button(down: false);
        Check(Receipts.Count(r => r.Kind == "down") == 1 && Receipts.Count(r => r.Kind == "up") == 1, "남은 물리 Up이 추가 대상 입력으로 전달됨");
        Check(_lensDowns == 0 && _status is { IsEnabled: false, WaitingForRelease: false }, "해제 뒤 자동 활성화 또는 추가 렌즈 클릭");
        _relay.RefreshFrame();
        Check(await _relay.StartAsync(), "물리 버튼 해제 뒤 명시적 재시작 실패");
        await _relay.StopAsync("경계 probe 완료");
        Results.Add(new { Name = "경계 Up 및 누른 버튼 해제 대기", Result = "PASS", LastTarget = lastTarget, Receipts = Receipts.ToArray() });
    }

    private static void ValidateDrag(ScreenPoint[] expected)
    {
        var downs = Receipts.Where(r => r.Kind == "down").ToArray();
        var ups = Receipts.Where(r => r.Kind == "up").ToArray();
        Check(downs.Length == 1 && ups.Length == 1, $"대상 Down/Up 수 불일치: {downs.Length}/{ups.Length}");
        Check(Near(downs[0].Point, expected[0]), "Down 좌표 불일치");
        Check(Near(ups[0].Point, expected[^1]), "Up 좌표 불일치");
        var moves = Receipts.Where(r => r.Kind == "move" && r.Pressed).ToArray();
        var index = 0;
        foreach (var expectedPoint in expected.Skip(1))
        {
            while (index < moves.Length && !Near(moves[index].Point, expectedPoint)) index++;
            Check(index < moves.Length, "연속 드래그 좌표 누락/순서 오류: " + expectedPoint);
            index++;
        }
        Check(!_targetPressed, "대상 메시지 누름 상태가 남음");
    }

    private static LensViewport Viewport()
    {
        _targetOrigin = ToPhysical(_target!, new Point(0, 0));
        var sourceStart = ToPhysical(_target!, new Point(30, 30));
        var sourceEnd = ToPhysical(_target!, new Point(290, 250));
        var destStart = ToPhysical(_lens!, new Point(20, 45));
        var destEnd = ToPhysical(_lens!, new Point(500, 415));
        return _currentViewport = new(new(sourceStart.X, sourceStart.Y, sourceEnd.X - sourceStart.X, sourceEnd.Y - sourceStart.Y),
            new(destStart.X, destStart.Y, destEnd.X - destStart.X, destEnd.Y - destStart.Y));
    }

    private static Window MakeWindow(string title, double x, double y, double width, double height, bool lens) => new()
    {
        Title = title, Left = x, Top = y, Width = width, Height = height,
        WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = lens,
        Topmost = true, ShowActivated = false, ShowInTaskbar = false,
        Background = lens ? Brushes.SteelBlue : Brushes.PapayaWhip,
        Content = new Border { BorderBrush = Brushes.DarkSlateGray, BorderThickness = new Thickness(3),
            Child = new TextBlock { Text = title + "\n자동 검증 중 · 마우스 조작 시 중단", Margin = new Thickness(12), FontSize = 16, Foreground = Brushes.Black } }
    };

    private static nint TargetMessages(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message is not (0x200 or 0x201 or 0x202)) return 0;
        if (message == 0x201) _targetPressed = true;
        var point = new ScreenPoint(_targetOrigin.X + (short)((long)lParam & 0xffff), _targetOrigin.Y + (short)(((long)lParam >> 16) & 0xffff));
        Receipts.Add(new(message == 0x200 ? "move" : message == 0x201 ? "down" : "up", point, _targetPressed, (long)GetMessageExtraInfo()));
        if (message == 0x202) _targetPressed = false;
        return 0;
    }

    private static nint LensMessages(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    { if (message == 0x201) _lensDowns++; return 0; }

    private static nint Guard(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var mouse = Marshal.PtrToStructure<MouseHook>(data);
            if (mouse.Extra != ProbeTag && mouse.Extra != RelayTag)
            {
                if (!_conflict)
                    Conflicts.Add(new { Kind = "mouse", Message = (long)message, mouse.Flags,
                        ExtraInfo = (long)mouse.Extra, X = mouse.Point.X, Y = mouse.Point.Y, ExpectedCommand = _lastCommand });
                _conflict = true;
                // 해당 이벤트를 받은 뒤 이어지는 검증 입력은 모두 중단한다.
                _ = _relay?.StopAsync("사용자 입력 감지 · probe 중단");
            }
        }
        return CallNextHookEx(0, code, message, data);
    }

    private static async Task Move(ScreenPoint point)
    {
        CheckOwnedForeground();
        Send(0xC001, point, ProbeTag);
        await Task.Delay(85);
        CheckOwnedForeground();
    }

    private static async Task Button(bool down)
    {
        CheckOwnedForeground();
        Send(down ? 2u : 4u, null, ProbeTag);
        _buttonSent = down;
        await Task.Delay(85);
        CheckOwnedForeground();
    }

    private static void Send(uint flags, ScreenPoint? point, nint tag)
    {
        if (tag == ProbeTag && point is not null) _lastProbePoint = point;
        _lastCommand = new { Number = ++_commandNumber, Flags = flags, Point = point, LastProbePoint = _lastProbePoint, ExtraInfo = (long)tag };
        var input = new NativeInput { Mouse = new() { Flags = flags, Extra = tag } };
        if (point is { } p)
        {
            input.Mouse.X = (int)(((long)p.X - GetSystemMetrics(76)) * 65536 / GetSystemMetrics(78) + 1);
            input.Mouse.Y = (int)(((long)p.Y - GetSystemMetrics(77)) * 65536 / GetSystemMetrics(79) + 1);
        }
        Check(SendInput(1, [input], Marshal.SizeOf<NativeInput>()) == 1, "probe SendInput 실패");
    }

    private static bool ButtonsDown() => new[] { 1, 2, 4, 5, 6 }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0);
    private static object DescribeAbi() => new { InputBytes = Marshal.SizeOf<NativeInput>(), MouseBytes = Marshal.SizeOf<MouseInput>(), MouseOffset = (long)Marshal.OffsetOf<NativeInput>(nameof(NativeInput.Mouse)), PointerBytes = IntPtr.Size };
    private static bool IsOwnedForeground() => GetForegroundWindow() == _targetHwnd || GetForegroundWindow() == _lensHwnd;
    private static void CheckOwnedForeground()
    {
        if (!IsOwnedForeground())
        {
            _conflict = true;
            Conflicts.Add(new { Kind = "foreground", Window = (long)GetForegroundWindow() });
        }
        Check(!_conflict, "사용자 입력 또는 다른 창 포커스 감지 · UI 검증 중단");
    }
    private static ScreenPoint ToPhysical(Window window, Point point) { var p = window.PointToScreen(point); return new((int)Math.Round(p.X), (int)Math.Round(p.Y)); }
    private static ScreenPoint DestinationPoint(LensViewport view, double x, double y) => new(view.Destination.X + (int)(view.Destination.Width * x), view.Destination.Y + (int)(view.Destination.Height * y));
    private static bool Near(ScreenPoint a, ScreenPoint b) => Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed record Receipt(string Kind, ScreenPoint Point, bool Pressed, long ExtraInfo);
    private delegate nint HookProc(int code, nint message, nint data);
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseHook { public NativePoint Point; public uint Data, Flags, Time; public nint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public MouseInput Mouse; }
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetMessageExtraInfo();
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int key);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, NativeInput[] input, int size);
    [DllImport("user32.dll")] private static extern nint SetWindowsHookEx(int kind, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
}
