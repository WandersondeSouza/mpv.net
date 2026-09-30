using System.Threading;

namespace MpvNet.Windows.UI;

// Gesture state only: mpv remains the authority for pause and fullscreen.
public sealed class VideoClickArbiter(int doubleClickTime)
{
    long _generation;
    long _startedAt;
    long _candidateGeneration;
    bool _eligible;
    bool _scheduled;

    public long Generation => Interlocked.Read(ref _generation);

    public long Begin(bool eligible, long now)
    {
        long generation = Invalidate();
        _candidateGeneration = generation;
        _startedAt = now;
        _eligible = eligible;
        _scheduled = false;
        return generation;
    }

    // Also callable from the mpv event thread when media changes.
    public long Invalidate() => Interlocked.Increment(ref _generation);

    public bool TrySchedule(long generation, long now, out int delay)
    {
        delay = 0;
        if (!_eligible || _scheduled || generation != _candidateGeneration || generation != Generation)
            return false;

        _scheduled = true;
        delay = (int)Math.Clamp(doubleClickTime - (now - _startedAt), 1, doubleClickTime);
        return true;
    }

    public bool TryComplete(long generation)
    {
        if (!_eligible || !_scheduled || generation != _candidateGeneration || generation != Generation)
            return false;

        return Interlocked.CompareExchange(ref _generation, generation + 1, generation) == generation;
    }
}
