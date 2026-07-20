namespace VideoSubtitleTranslator.Core.Models;

public class TranscriptionResult
{
    public IReadOnlyList<Segment> Segments { get; init; } = Array.Empty<Segment>();
    public string DetectedLanguage { get; init; } = "en";
}
