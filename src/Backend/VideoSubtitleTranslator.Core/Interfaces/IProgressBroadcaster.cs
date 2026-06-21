using VideoSubtitleTranslator.Core.Events;

namespace VideoSubtitleTranslator.Core.Interfaces;

public interface IProgressBroadcaster
{
    Task BroadcastAsync(JobProgressEvent progress, CancellationToken ct = default);
    IAsyncEnumerable<JobProgressEvent> SubscribeAsync(string jobId, CancellationToken ct = default);
}
