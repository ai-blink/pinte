namespace Magnifier.Core;

public sealed class PointerInputSession
{
    private readonly IPointerInput _pointerInput;
    private bool _isInputEnabled;
    private bool _isPressed;

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
            _pointerInput.MoveTo(point);
            LastPoint = point;
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
