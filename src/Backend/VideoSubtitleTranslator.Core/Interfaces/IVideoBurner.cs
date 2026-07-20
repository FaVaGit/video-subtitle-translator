namespace VideoSubtitleTranslator.Core.Interfaces;

public interface IVideoBurner
{
    Task<string> BurnSubtitlesAsync(
        string videoPath,
        string subtitlePath,
        string outputPath,
        CancellationToken ct = default);
}
