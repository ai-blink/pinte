using System.Runtime.InteropServices;
using Magnifier.Core;

internal static partial class RelayProbe
{
    // Only this diagnostic's tagged motion is suppressed; other assistive input is untouched.
    private const nint SuppressedRestoreTag = 0x52535452;
    private static bool _cursorDiagnostic;
    private static int _suppressedRestoreCount;

    private static async Task CursorDiagnostic()
    {
        var view = Viewport();
        await _relay!.ConfigureAsync(view, _lensHwnd);
        _relay.RefreshFrame();
        Check(await _relay.StartAsync(), "커서 진단 시작 실패");
        var lensPoint = DestinationPoint(view, .45, .45);
        var sourcePoint = view.MapToSource(new(lensPoint.X, lensPoint.Y));
        await Move(lensPoint);
        var currentCursor = ReadCursor();
        Check(_status is { IsEnabled: true }, "조작 요청 준비 실패");
        Results.Add(new { Name = "클릭 전 실제 커서와 렌즈 위치", LensPoint = lensPoint,
            SourcePoint = sourcePoint, SystemCursor = currentCursor, LogicalCursor = _status!.Position,
            SamePosition = Near(currentCursor, lensPoint) });

        await Button(down: true);
        await WaitForTargetPress();
        Check(_targetPressed, "진단 대상 Down 수신 실패");
        Results.Add(new { Name = "드래그 중 실제 커서와 렌즈 포인터", LensPoint = lensPoint,
            SourcePoint = sourcePoint, SystemCursor = ReadCursor(), LogicalCursor = _status!.Position,
            SamePosition = Near(ReadCursor(), lensPoint) });
        var receiptsBefore = Receipts.Count;
        CheckOwnedForeground();
        Send(0xC001, lensPoint, SuppressedRestoreTag);
        await Task.Delay(100);
        CheckOwnedForeground();
        Results.Add(new { Name = "복귀 SendInput을 hook에서 억제", SystemCursor = ReadCursor(),
            CursorReturned = Near(ReadCursor(), lensPoint), Suppressed = _suppressedRestoreCount,
            TargetMoves = Receipts.Skip(receiptsBefore).Where(r => r.Kind == "move" && r.Pressed).ToArray() });

        receiptsBefore = Receipts.Count;
        Check(SetCursorPos(lensPoint.X, lensPoint.Y), "진단 SetCursorPos 실패");
        await Task.Delay(100);
        CheckOwnedForeground();
        var restoreMoves = Receipts.Skip(receiptsBefore).Where(r => r.Kind == "move" && r.Pressed).ToArray();
        Results.Add(new { Name = "드래그 중 SetCursorPos로 렌즈 복귀", SystemCursor = ReadCursor(),
            CursorReturned = Near(ReadCursor(), lensPoint), TargetMoves = restoreMoves,
            UnwantedStroke = restoreMoves.Any(r => !Near(r.Point, sourcePoint)) });

        // Return to the last valid target before Up. This is confined to our captured test window.
        CheckOwnedForeground();
        Send(0xC001, sourcePoint, RelayTag);
        await Task.Delay(50);
        CheckOwnedForeground();
        await _relay.StopAsync("커서 진단 종료 · 대상 해제");
        await Button(down: false);
        Check(!_targetPressed, "커서 진단 종료 뒤 대상 누름 잔류");
    }

    private static ScreenPoint ReadCursor()
    {
        Check(GetCursorPos(out var point), "실제 커서 좌표 읽기 실패");
        return new(point.X, point.Y);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);
}
