using System.Collections.Concurrent;

namespace Magnifier.Infrastructure;

internal sealed class RelayCommandPump
{
    private readonly ConcurrentQueue<(Action Action, bool LeftRelease)> _pending = new();
    private int _leftReleases;
    public bool HasPendingLeftRelease => Volatile.Read(ref _leftReleases) != 0;

    public void Enqueue(Action action, bool leftRelease = false)
    {
        if (leftRelease) Interlocked.Increment(ref _leftReleases);
        _pending.Enqueue((action, leftRelease));
    }

    public void ProcessMessage(bool timer, Action checkSession)
    {
        // A hook can queue Up while GetMessage is returning an already-selected timer.
        // Apply all observed input before comparing a native capture snapshot with session state.
        while (_pending.TryDequeue(out var command))
        {
            try { command.Action(); }
            finally { if (command.LeftRelease) Interlocked.Decrement(ref _leftReleases); }
        }
        if (timer) checkSession();
    }
}
