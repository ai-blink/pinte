namespace Magnifier.Core;

/// <summary>직접 조작의 명시적 시작, 누름 해제, 물리 버튼 재무장을 관리한다.</summary>
public sealed class LensInputState
{
    private readonly PointerInputSession _session;
    private bool _physicalButtonDown;

    public LensInputState(IPointerInput pointerInput)
    {
        _session = new PointerInputSession(pointerInput);
    }

    public bool IsEnabled => _session.IsInputEnabled;

    public bool IsPressed => _session.IsPressed;

    public bool IsWaitingForRelease { get; private set; }

    public ScreenPoint? LastTarget => _session.LastPoint;

    public bool Arm(bool physicalButtonDown)
    {
        _physicalButtonDown = physicalButtonDown;
        if (physicalButtonDown)
        {
            IsWaitingForRelease = true;
            return false;
        }

        if (IsPressed || IsWaitingForRelease)
        {
            return false;
        }

        _session.SetInputEnabled(true);
        return true;
    }

    /// <param name="down">기본 물리 버튼 중 하나라도 눌렸으면 true. 자체 합성 입력은 제외한다.</param>
    public void ObservePhysicalButton(bool down)
    {
        _physicalButtonDown = down;
        if (!down)
        {
            IsWaitingForRelease = false;
        }
    }

    public bool Begin(ScreenPoint point)
    {
        if (!IsEnabled || IsWaitingForRelease)
        {
            return false;
        }

        try
        {
            return _session.Begin(point);
        }
        catch
        {
            StopAfterFailure();
            throw;
        }
    }

    public void Move(ScreenPoint point)
    {
        try
        {
            _session.Move(point);
        }
        catch
        {
            StopAfterFailure();
            throw;
        }
    }

    public void Complete(ScreenPoint point)
    {
        try
        {
            _session.Complete(point);
        }
        catch
        {
            StopAfterFailure();
            throw;
        }
    }

    public void Stop()
    {
        IsWaitingForRelease |= _physicalButtonDown;
        _session.SetInputEnabled(false);
    }

    private void StopAfterFailure()
    {
        IsWaitingForRelease |= _physicalButtonDown;
        // PointerInputSession 자체가 입력 게이트를 닫고 Up을 시도한다.
        // 이미 실패한 Up을 여기서 즉시 반복하지 않고 Stop에서 재시도한다.
        if (_session.IsInputEnabled)
        {
            try
            {
                _session.SetInputEnabled(false);
            }
            catch
            {
                // 첫 오류를 유지하며 미해제 상태는 Stop의 재시도를 위해 남긴다.
            }
        }
    }
}
