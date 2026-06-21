using VideoSubtitleTranslator.Core.Enums;

namespace VideoSubtitleTranslator.Core.Events;

public record JobProgressEvent
{
    public string JobId { get; init; } = string.Empty;
    public JobStatus Status { get; init; }
    public int ProgressPercent { get; init; }
    public string Stage { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
