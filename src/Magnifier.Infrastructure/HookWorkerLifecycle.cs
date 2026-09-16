namespace Magnifier.Infrastructure;

// A low-level hook belongs to the installing message thread. Once Windows silently
// removes that hook, recreating only its handle on the same thread is not a recovery.
// This small state owner makes the worker reincarnation boundary explicit and prevents
// overlapping replacement requests from creating competing hook threads.
internal sealed class HookWorkerLifecycle
{
    private bool _replacementPending;

    public int Generation { get; private set; } = 1;

    public bool ReplacementPending => _replacementPending;

    public bool TryBeginReplacement()
    {
        if (_replacementPending) return false;
        _replacementPending = true;
        return true;
    }

    public int CompleteReplacement()
    {
        if (!_replacementPending) throw new InvalidOperationException("교체 요청 없이 입력 훅 스레드를 시작할 수 없습니다.");
        _replacementPending = false;
        return ++Generation;
    }

    public void CancelReplacement() => _replacementPending = false;
}
