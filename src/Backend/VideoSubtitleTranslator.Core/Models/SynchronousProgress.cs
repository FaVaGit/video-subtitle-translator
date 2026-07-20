namespace VideoSubtitleTranslator.Core.Models;

/// <summary>
/// An <see cref="IProgress{T}"/> implementation that invokes the callback
/// synchronously on the calling thread. Unlike <see cref="Progress{T}"/>,
/// which posts to a captured <see cref="SynchronizationContext"/> (or the
/// thread pool when none exists, as in ASP.NET/Generic Host apps), this
/// guarantees progress events are observed in the exact order they are
/// reported, which matters for SSE progress streaming.
/// </summary>
public sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public SynchronousProgress(Action<T> handler)
    {
        _handler = handler;
    }

    public void Report(T value) => _handler(value);
}
