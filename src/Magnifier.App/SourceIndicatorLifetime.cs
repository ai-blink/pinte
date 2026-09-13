namespace Magnifier.App;

// Dispatcher 지연이나 오래된 Tick은 현재 기한만 확인한다. 새 세션의 선을 숨기지 않는다.
internal sealed class SourceIndicatorLifetime(TimeProvider clock)
{
    private DateTimeOffset? _deadline;
    private bool _active;

    public void Start(SourceIndicatorPreference preference)
    {
        _active = preference != SourceIndicatorPreference.Hidden;
        _deadline = preference == SourceIndicatorPreference.Brief
            ? clock.GetUtcNow() + TimeSpan.FromSeconds(5) : null;
    }

    public bool IsVisible => _active && (_deadline is null || clock.GetUtcNow() < _deadline);
    public bool IsBrief => _active && _deadline is not null;
    public void Cancel() { _active = false; _deadline = null; }
}
