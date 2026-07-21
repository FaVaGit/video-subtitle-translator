using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Core.Events;

public record JobCreatedEvent
{
    public string JobId { get; init; } = string.Empty;
    public string VideoPath { get; init; } = string.Empty;
    public ProcessingOptions Options { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When set, generated files (subtitles, burned video) are written directly
    /// into this folder instead of the app-managed job storage directory.
    /// Used for local-path processing so output lands next to the input file.
    /// </summary>
    public string? OutputDirectory { get; init; }

    /// <summary>
    /// Path of the JSON snapshot file containing the latest known progress state
    /// for this job. Used as a reliable fallback when live event delivery lags.
    /// </summary>
    public string? ProgressFilePath { get; init; }
}
