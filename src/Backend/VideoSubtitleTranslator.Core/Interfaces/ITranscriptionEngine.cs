using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Core.Interfaces;

public interface ITranscriptionEngine
{
    Task<TranscriptionResult> TranscribeAsync(
        string audioPath,
        string? sourceLanguage,
        string modelSize,
        IProgress<int>? progress = null,
        CancellationToken ct = default);
}
