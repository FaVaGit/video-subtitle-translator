using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Core.Interfaces;

public interface ITranscriptionEngine
{
    Task<IReadOnlyList<Segment>> TranscribeAsync(
        string audioPath,
        string? sourceLanguage,
        IProgress<int>? progress = null,
        CancellationToken ct = default);
}
