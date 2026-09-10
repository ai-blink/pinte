namespace Magnifier.Core;

/// <summary>조작 요청을 일시 정지와 분리하고, 누름 해제 뒤 재개를 관리한다.</summary>
public sealed class LensInputState
{
    private readonly PointerInputSession _session;
    private bool _physicalButtonDown;

    public LensInputState(IPointerInput pointerInput)
    {
        _session = new PointerInputSession(pointerInput);
    }

    public bool IsEnabled => _session.IsInputEnabled;

    public bool IsRequested { get; private set; }

    public void RequestStart() => IsRequested = true;

    public bool TryResume(bool physicalButtonDown)
    {
        if (!IsRequested || IsEnabled || physicalButtonDown || _physicalButtonDown
            || IsPressed || IsWaitingForRelease) return false;
        return Arm(false);
    }

    public bool IsPressed => _session.IsPressed;

    public bool IsWaitingForRelease { get; private set; }

    public ScreenPoint? LastTarget => _session.LastPoint;

    public bool Arm(bool physicalButtonDown)
    {
        RequestStart();
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

    public void Stop() => Stop(ownsPhysicalPress: true);

    /// <param name="ownsPhysicalPress">false for a WPF window handle press that was never relayed.</param>
    public void Stop(bool ownsPhysicalPress)
    {
        IsRequested = false;
        Pause(ownsPhysicalPress);
    }

    public void Pause(bool ownsPhysicalPress = true)
    {
        // A window thumb must retain its capture while geometry stops the relay.
        // A real target press always drains, even if the caller says otherwise.
        IsWaitingForRelease |= _physicalButtonDown && (ownsPhysicalPress || IsPressed);
        try { _session.SetInputEnabled(false); }
        catch { IsRequested = false; throw; }
    }

    private void StopAfterFailure()
    {
        IsRequested = false;
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
