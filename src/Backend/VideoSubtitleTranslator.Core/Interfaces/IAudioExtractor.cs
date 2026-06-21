namespace VideoSubtitleTranslator.Core.Interfaces;

public interface IAudioExtractor
{
    Task<string> ExtractAudioAsync(string videoPath, string outputDir, CancellationToken ct = default);
}
