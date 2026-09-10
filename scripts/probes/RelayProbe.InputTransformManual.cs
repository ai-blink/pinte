using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Ellipse = System.Windows.Shapes.Ellipse;
using System.Windows.Threading;
using Magnifier.Core;
using Magnifier.Infrastructure;

internal static partial class RelayProbe
{
    // 실제 장치 입력을 관찰한다. 이 모드는 SendInput, 커서 복귀, 자동 클릭을 실행하지 않는다.
    private static async Task RunInputTransformManual(Application app)
    {
        WindowsInputTransform? transform = null;
        DispatcherTimer? timer = null;
        var finish = new TaskCompletionSource<string>();
        var samples = new List<object>();
        var routedDowns = 0;
        var reason = "ERROR";
        string? error = null;
        var disabled = false;
        var closing = false;
        try
        {
            Check(!ButtonsDown(), "마우스 버튼을 놓고 시작해야 합니다.");
            var area = SystemParameters.WorkArea;
            var left = area.Left + Math.Max(20, (area.Width - 900) / 2);
            _target = MakeWindow("OS 입력 원본 수신 확인", left, area.Bottom - 350, 320, 280, false);
            _lens = MakeWindow("OS 입력 렌즈 · 실제 마우스 확인", left + 350, area.Bottom - 500, 520, 440, true);
            var targetCanvas = new Canvas { Background = Brushes.PapayaWhip };
            var lensCanvas = new Canvas { Background = Brushes.SteelBlue };
            var meter = new TextBlock { FontSize = 17, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap, Width = 285 };
            Canvas.SetLeft(meter, 15); Canvas.SetTop(meter, 10); targetCanvas.Children.Add(meter);
            var sourceDot = new Ellipse { Width = 12, Height = 12, Fill = Brushes.Red };
            Canvas.SetLeft(sourceDot, 154); Canvas.SetTop(sourceDot, 134); targetCanvas.Children.Add(sourceDot);
            var lensDot = new Ellipse { Width = 46, Height = 46, Fill = Brushes.Red };
            Canvas.SetLeft(lensDot, 237); Canvas.SetTop(lensDot, 207); lensCanvas.Children.Add(lensDot);
            var instruction = new TextBlock { Text = "빨간 원을 클릭하고\n조금 드래그한 뒤 버튼을 놓으세요.",
                FontSize = 22, Foreground = Brushes.White, Width = 450, TextAlignment = TextAlignment.Center };
            Canvas.SetLeft(instruction, 35); Canvas.SetTop(instruction, 75); lensCanvas.Children.Add(instruction);
            var stop = new Button { Content = "검증 종료", Width = 150, Height = 38, FontSize = 18 };
            Canvas.SetLeft(stop, 350); Canvas.SetTop(stop, 3); lensCanvas.Children.Add(stop);
            stop.Click += (_, _) => finish.TrySetResult("USER_FINISHED");
            _target.Content = targetCanvas;
            _lens.Content = lensCanvas;
            _target.Show(); _lens.Show();
            _targetHwnd = new WindowInteropHelper(_target).Handle;
            _lensHwnd = new WindowInteropHelper(_lens).Handle;
            var view = Viewport();
            var lensOrigin = ToPhysical(_lens, new Point(0, 0));
            HwndSource.FromHwnd(_targetHwnd)!.AddHook(TargetMessages);
            HwndSource.FromHwnd(_targetHwnd)!.AddHook(ObserveTarget);
            HwndSource.FromHwnd(_lensHwnd)!.AddHook(ObserveLens);
            _target.MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Left) _target.CaptureMouse(); };
            _target.MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Left) _target.ReleaseMouseCapture(); };
            _target.Closing += (_, e) => { if (!closing) { e.Cancel = true; finish.TrySetResult("WINDOW_CLOSED"); } };
            _lens.Closing += (_, e) => { if (!closing) { e.Cancel = true; finish.TrySetResult("WINDOW_CLOSED"); } };
            _lens.Activate();
            await Task.Delay(180);
            CheckOwnedForeground();
            transform = new WindowsInputTransform();
            transform.Enable(view);
            var expires = Environment.TickCount64 + 300_000;
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (_, _) =>
            {
                meter.Text = $"원본 수신: {Receipts.Count(r => r.Kind == "down")}회\n렌즈 자체 수신: {_lensDowns}회\n렌즈 위치에서 원본에 도착: {routedDowns}회";
                if (!IsOwnedForeground()) finish.TrySetResult("FOCUS_LOST");
                if (Environment.TickCount64 >= expires) finish.TrySetResult("TIMEOUT");
            };
            timer.Start();
            reason = await finish.Task;
            await Task.Delay(40); // 종료 버튼의 실제 Up 처리가 완료된 뒤 변환 해제.

            nint ObserveTarget(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
            {
                if (message is not (0x201 or 0x202) && !(message == 0x200 && _targetPressed)) return 0;
                var physical = ReadPhysicalCursor();
                var received = new ScreenPoint(_targetOrigin.X + (short)((long)lParam & 0xffff),
                    _targetOrigin.Y + (short)(((long)lParam >> 16) & 0xffff));
                var expected = view.MapToSource(new(physical.X, physical.Y));
                var fromLens = view.Contains(physical);
                if (message == 0x201 && fromLens && Near(received, expected)) routedDowns++;
                if (samples.Count < 2000) samples.Add(new { Surface = "target", Message = message,
                    Physical = physical, Received = received, Expected = expected, FromLens = fromLens });
                return 0;
            }
            nint ObserveLens(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
            {
                if (message is not (0x201 or 0x202)) return 0;
                var point = new ScreenPoint(lensOrigin.X + (short)((long)lParam & 0xffff),
                    lensOrigin.Y + (short)(((long)lParam >> 16) & 0xffff));
                if (!view.Contains(point)) return 0; // 상단 종료 버튼은 판정에서 제외한다.
                if (message == 0x201) _lensDowns++;
                if (samples.Count < 2000) samples.Add(new { Surface = "lens", Message = message,
                    Physical = ReadPhysicalCursor(), Received = point });
                return 0;
            }
        }
        catch (Exception ex) { error = ex.Message; }
        finally
        {
            timer?.Stop();
            var released = !ButtonsDown() && !_targetPressed;
            try { transform?.Disable(); disabled = transform is null || !transform.IsEnabled; }
            catch (Exception ex) { error += " / Disable: " + ex.Message; }
            try { transform?.Dispose(); }
            catch (Exception ex) { error += " / Dispose: " + ex.Message; disabled = false; }
            closing = true;
            _lens?.Close(); _target?.Close();
            var result = error is not null ? "BLOCKED_ERROR"
                : reason != "USER_FINISHED" ? "NEEDS_USER_UI_CHECK"
                : routedDowns > 0 && _lensDowns == 0 ? "TARGET_RESPONSE_OBSERVED_NEEDS_DRAG_CHECK"
                : _lensDowns > 0 ? "BLOCKED_OS_MOUSE_ROUTING" : "NEEDS_USER_UI_CHECK";
            if (!disabled || !released) result = "BLOCKED_CLEANUP";
            var report = new { Result = result, Reason = reason, Error = error,
                Executable = Environment.ProcessPath, EngineBuild = typeof(WindowsInputTransform).Assembly.ManifestModule.ModuleVersionId,
                Viewport = _currentViewport, RoutedDowns = routedDowns, LensDowns = _lensDowns,
                Receipts, Samples = samples, Released = released, TransformDisabled = disabled };
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Magnifier", "diagnostics");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, $"input-transform-manual-{Environment.ProcessId}.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            app.Shutdown(result.StartsWith("TARGET_RESPONSE") ? 0 : 2);
        }
    }
}
