using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Magnifier.Core;
using Magnifier.Infrastructure;

internal static partial class RelayProbe
{
    private static async Task RunInputTransform(Application app, bool nativeSurface = false)
    {
        WindowsInputTransform? transform = null;
        WindowsMagnifierSurface? surface = null;
        DispatcherTimer? surfaceTimer = null;
        Exception? surfaceError = null;
        nint nativeHost = 0;
        var mappingEnabled = false;
        var result = "FAIL";
        string? error = null;
        var cursorSamples = new List<object>();
        var released = false;
        var disabled = false;
        var cursorMatched = true;
        try
        {
            _originalCursor = ReadPhysicalCursor();
            Check(!ButtonsDown(), "실행 전에 물리 버튼이 눌려 있음");
            var area = SystemParameters.WorkArea;
            Check(area.Width >= 1100 && area.Height >= 650, "검증 창 두 개를 배치할 화면 공간 부족");
            _target = MakeWindow("Magnifier OS 변환 검증 대상", area.Left + 50, area.Top + 70, 320, 280, false);
            _lens = MakeWindow("Magnifier OS 변환 검증 렌즈", area.Left + 470, area.Top + 70, 520, 440, true);
            _target.Show();
            _lens.Show();
            _targetHwnd = new WindowInteropHelper(_target).Handle;
            _lensHwnd = new WindowInteropHelper(_lens).Handle;
            HwndSource.FromHwnd(_targetHwnd)!.AddHook(TargetMessages);
            HwndSource.FromHwnd(_lensHwnd)!.AddHook(LensMessages);
            _target.MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Left) _target.CaptureMouse(); };
            _target.MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Left) _target.ReleaseMouseCapture(); };
            _target.Activate();
            await Task.Delay(180);
            CheckOwnedForeground();
            _guard = SetWindowsHookEx(14, GuardProc, GetModuleHandle(null), 0);
            Check(_guard != 0, "공유 데스크톱 충돌 감시 설치 실패");
            transform = new WindowsInputTransform();
            var view = Viewport();
            if (nativeSurface)
            {
                // WPF의 HWND 스타일 갱신과 분리한 실험용 네이티브 부모. 제품 창은 바꾸지 않는다.
                var origin = ToPhysical(_lens, new Point(0, 0));
                var end = ToPhysical(_lens, new Point(_lens.Width, _lens.Height));
                nativeHost = CreateNativeHost(origin, end);
                Check(nativeHost != 0, "네이티브 부모 생성 실패: " + Marshal.GetLastPInvokeError());
                Check(SetLayeredWindowAttributes(nativeHost, 0, 255, 2),
                    "네이티브 부모 불투명 속성 설정 실패: " + Marshal.GetLastPInvokeError());
                _lensHwnd = nativeHost;
                _lens.Hide();
                surface = new WindowsMagnifierSurface(nativeHost, view, origin);
                ShowWindow(nativeHost, 4);
                surfaceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
                surfaceTimer.Tick += (_, _) =>
                {
                    try { surface.Refresh(); }
                    catch (Exception ex) { surfaceError = ex; surfaceTimer.Stop(); }
                };
                surfaceTimer.Start();
            }
            transform.Enable(view);
            mappingEnabled = true;
            Receipts.Clear();
            _lensDowns = 0;
            var path = new[] { (.20, .20), (.35, .25), (.50, .45), (.65, .65), (.50, .45), (.35, .25), (.20, .20) };
            var points = path.Select(p => DestinationPoint(view, p.Item1, p.Item2)).ToArray();
            var expected = points.Select(p => view.MapToSource(new(p.X, p.Y))).ToArray();
            await Move(points[0]);
            Sample("hover", points[0], expected[0]);
            var hit = WindowFromPoint(new NativePoint { X = points[0].X, Y = points[0].Y });
            Check(hit == _lensHwnd || hit == surface?.Handle,
                "클릭 전 검증 렌즈가 입력 위치에 없음: HWND=" + hit);
            await Button(down: true);
            Sample("down", points[0], expected[0]);
            // 실패하더라도 이 프로브의 버튼은 먼저 놓고 판정한다.
            if (Receipts.Count(r => r.Kind == "down") == 1)
            {
                for (var i = 1; i < points.Length; i++)
                {
                    await Move(points[i]);
                    Sample("move", points[i], expected[i]);
                }
            }
            await Button(down: false);
            Sample("up", points[^1], expected[^1]);
            if (surfaceError is not null) throw surfaceError;
            ValidateDrag(expected);
            Check(_lensDowns == 0, "OS 변환 후 렌즈가 Down을 수신함");
            Check(cursorMatched, "OS 변환 중 물리 커서가 렌즈 입력 위치를 벗어남");
            result = "PASS_SYNTHETIC_INPUT_ONLY";

            void Sample(string phase, ScreenPoint lens, ScreenPoint source)
            {
                var physical = ReadPhysicalCursor();
                cursorMatched &= Near(physical, lens);
                cursorSamples.Add(new
                {
                    Phase = phase, Lens = lens, ExpectedSource = source,
                    Cursor = ReadCursor(), PhysicalCursor = physical,
                    WindowUnderCursor = (long)WindowFromPoint(new NativePoint { X = physical.X, Y = physical.Y }),
                    MagnifierSurface = (long)(surface?.Handle ?? 0),
                    TargetPressed = _targetPressed, LensDowns = _lensDowns
                });
            }
        }
        catch (Exception ex)
        {
            result = _conflict ? "NEEDS_USER_UI_CHECK" : nativeSurface && !mappingEnabled
                ? "BLOCKED_NATIVE_SURFACE_SETUP" : "BLOCKED_OS_MOUSE_ROUTING";
            error = ex.Message;
            Environment.ExitCode = 2;
        }
        finally
        {
            // Keep mapping until the owned button is up. Never move a pressed cursor for cleanup.
            if (_buttonSent)
            {
                // 사용자 충돌 뒤에도 프로브가 보낸 Down의 Up만 정리한다. 이동·클릭·포커스 변경은 없다.
                try { Send(4, null, RelayTag); _buttonSent = false; await Task.Delay(85); }
                catch (Exception ex) { error += " / Up: " + ex.Message; }
            }
            released = !_buttonSent && !_targetPressed;
            surfaceTimer?.Stop();
            try { transform?.Disable(); disabled = transform is null || !transform.IsEnabled; }
            catch (Exception ex) { error += " / Disable: " + ex.Message; }
            try { surface?.Dispose(); }
            catch (Exception ex) { error += " / Surface: " + ex.Message; }
            if (nativeHost != 0 && !DestroyWindow(nativeHost)) error += " / Host: " + Marshal.GetLastPInvokeError();
            try { transform?.Dispose(); }
            catch (Exception ex) { error += " / Dispose: " + ex.Message; disabled = false; }
            if (_guard != 0) UnhookWindowsHookEx(_guard);
            _lens?.Close();
            _target?.Close();
            if (!_conflict && released && disabled) Send(0xC001, _originalCursor, ProbeTag);
            if (!released || !disabled) { result = "BLOCKED_CLEANUP"; Environment.ExitCode = 2; }
            var report = new { Result = result, Error = error, NativeSurface = nativeSurface,
                TargetWindow = (long)_targetHwnd, LensWindow = (long)_lensHwnd, Executable = Environment.ProcessPath,
                EngineBuild = typeof(WindowsInputTransform).Assembly.ManifestModule.ModuleVersionId,
                Viewport = _currentViewport, Receipts, CursorSamples = cursorSamples, LensDowns = _lensDowns,
                Released = released, TransformDisabled = disabled,
                PhysicalCursorMatched = cursorSamples.Count > 0 ? (bool?)cursorMatched : null, Conflicts };
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Magnifier", "diagnostics");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, $"input-transform-pointer-{Environment.ProcessId}.json"), json);
            Console.WriteLine(json);
            app.Shutdown(Environment.ExitCode);
        }
    }

    private static ScreenPoint ReadPhysicalCursor()
    {
        Check(GetPhysicalCursorPos(out var point), "물리 커서 좌표 조회 실패");
        return new(point.X, point.Y);
    }

    [DllImport("user32.dll")] private static extern bool GetPhysicalCursorPos(out NativePoint point);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(nint hwnd, uint color, byte alpha, uint flags);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint extendedStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyWindow(nint hwnd);
}
