using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Core.Interfaces;

public interface ISubtitleGenerator
{
    Task<string> GenerateSrtAsync(IReadOnlyList<Segment> segments, string outputPath, CancellationToken ct = default);
    Task<string> GenerateVttAsync(IReadOnlyList<Segment> segments, string outputPath, CancellationToken ct = default);
}
