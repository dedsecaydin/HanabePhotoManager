namespace HanabePhotoManager.Core.Performance;

public sealed class ThrottledProgress<T> : IProgress<T>
{
    private readonly IProgress<T> _inner;
    private readonly TimeSpan _minimumInterval;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();
    private DateTimeOffset? _lastReportAt;

    public ThrottledProgress(IProgress<T> inner, TimeSpan minimumInterval)
        : this(inner, minimumInterval, () => DateTimeOffset.UtcNow)
    {
    }

    public ThrottledProgress(Action<T> report, TimeSpan minimumInterval, Func<DateTimeOffset>? clock = null)
        : this(new DelegateProgress(report), minimumInterval, clock ?? (() => DateTimeOffset.UtcNow))
    {
    }

    private ThrottledProgress(IProgress<T> inner, TimeSpan minimumInterval, Func<DateTimeOffset> clock)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(clock);
        if (minimumInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumInterval));
        }

        _inner = inner;
        _minimumInterval = minimumInterval;
        _clock = clock;
    }

    public void Report(T value)
    {
        lock (_gate)
        {
            var now = _clock();
            if (_lastReportAt is { } previous && now - previous < _minimumInterval)
            {
                return;
            }

            _lastReportAt = now;
        }

        _inner.Report(value);
    }

    private sealed class DelegateProgress(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
