using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Core.Interfaces;

public interface ITranslationService
{
    Task<IReadOnlyList<Segment>> TranslateAsync(
        IReadOnlyList<Segment> segments,
        string targetLanguage,
        IProgress<int>? progress = null,
        CancellationToken ct = default);
}
