using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Core.Events;

public record JobCreatedEvent
{
    public string JobId { get; init; } = string.Empty;
    public string VideoPath { get; init; } = string.Empty;
    public ProcessingOptions Options { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
