using VideoSubtitleTranslator.Core.Events;

namespace VideoSubtitleTranslator.Core.Interfaces;

public interface IJobPublisher
{
    Task PublishJobAsync(JobCreatedEvent job, CancellationToken ct = default);
    Task PublishProgressAsync(JobProgressEvent progress, CancellationToken ct = default);
}
