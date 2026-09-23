namespace Magnifier.Core;

public sealed class PointerInputSession
{
    private readonly IPointerInput _pointerInput;
    private bool _isInputEnabled;
    private bool _isPressed;
    private bool _hasPreparedPress;

    public PointerInputSession(IPointerInput pointerInput)
    {
        _pointerInput = pointerInput ?? throw new ArgumentNullException(nameof(pointerInput));
    }

    public bool IsInputEnabled => _isInputEnabled;

    public bool IsPressed => _isPressed;

    public ScreenPoint? LastPoint { get; private set; }

    public void SetInputEnabled(bool isEnabled)
    {
        if (isEnabled)
        {
            if (_isPressed && !_isInputEnabled)
            {
                throw new InvalidOperationException("해제하지 못한 입력을 먼저 취소해야 합니다.");
            }

            _isInputEnabled = true;
            return;
        }

        try
        {
            Cancel();
        }
        finally
        {
            _isInputEnabled = false;
        }
    }

    public bool Begin(ScreenPoint point)
    {
        return PrepareBegin(point) && BeginPrepared();
    }

    /// <summary>버튼을 누르지 않고 원본 위치로 이동한다. 취소하면 준비도 무효화된다.</summary>
    public bool PrepareBegin(ScreenPoint point)
    {
        if (!_isInputEnabled)
        {
            return false;
        }

        if (_isPressed)
        {
            throw new InvalidOperationException("입력 세션이 이미 진행 중입니다.");
        }

        try
        {
            _hasPreparedPress = false;
            _pointerInput.MoveTo(point);
            LastPoint = point;
            _hasPreparedPress = true;
            return true;
        }
        catch
        {
            TryReleaseAfterFailure();
            throw;
        }
    }

    /// <summary>준비한 위치에서 재이동 없이 Down을 보낸다. 호출자는 위치와 대기 시간을 확인한다.</summary>
    public bool BeginPrepared()
    {
        if (!_isInputEnabled) return false;
        if (_isPressed || !_hasPreparedPress)
            throw new InvalidOperationException("누름 전에 원본 위치를 준비해야 합니다.");
        _hasPreparedPress = false;
        try
        {
            // Down 실패도 일부 입력이 전달되었을 수 있으므로 release 대상이다.
            _isPressed = true;
            _pointerInput.LeftButtonDown();
            return true;
        }
        catch
        {
            TryReleaseAfterFailure();
            throw;
        }
    }

    public void Move(ScreenPoint point)
    {
        if (!_isPressed || !_isInputEnabled)
        {
            return;
        }

        try
        {
            _pointerInput.MoveTo(point);
            LastPoint = point;
        }
        catch
        {
            TryReleaseAfterFailure();
            throw;
        }
    }

    public void Complete(ScreenPoint point)
    {
        if (!_isPressed)
        {
            return;
        }

        if (!_isInputEnabled)
        {
            Cancel();
            return;
        }

        try
        {
            _pointerInput.MoveTo(point);
            LastPoint = point;
        }
        catch
        {
            TryReleaseAfterFailure();
            throw;
        }

        Release();
    }

    public void Cancel()
    {
        _hasPreparedPress = false;
        if (_isPressed)
        {
            Release();
        }
    }

    private void TryReleaseAfterFailure()
    {
        _isInputEnabled = false;

        try
        {
            Cancel();
        }
        catch
        {
            // 원래 입력 실패를 보존하고 IsPressed를 남겨 다음 Cancel에서 재시도한다.
        }
    }

    private void Release()
    {
        try
        {
            _pointerInput.LeftButtonUp();
            _isPressed = false;
        }
        catch
        {
            _isInputEnabled = false;
            throw;
        }
    }
}
