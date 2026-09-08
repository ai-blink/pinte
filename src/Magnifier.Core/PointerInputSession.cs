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

    public void SetInputEnabled(bool isEnabled)
    {
        if (isEnabled)
        {
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

        _pointerInput.MoveTo(point);
        _isPressed = true;

        try
        {
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
        if (!_isPressed)
        {
            return;
        }

        try
        {
            _pointerInput.MoveTo(point);
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

        try
        {
            _pointerInput.MoveTo(point);
        }
        finally
        {
            Release();
        }
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
        try
        {
            Release();
        }
        catch
        {
            // 원래 입력 실패를 보존하되, release는 반드시 한 번 시도한다.
        }
    }

    private void Release()
    {
        try
        {
            _pointerInput.LeftButtonUp();
        }
        finally
        {
            _isPressed = false;
        }
    }
}
