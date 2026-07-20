using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Tests.TestDoubles;

public class FakeAudioExtractor : IAudioExtractor
{
    public Task<string> ExtractAudioAsync(string videoPath, string outputDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);
        var audioPath = Path.Combine(outputDir, "audio.wav");
        File.WriteAllText(audioPath, "fake-audio");
        return Task.FromResult(audioPath);
    }
}

public class FakeTranscriptionEngine : ITranscriptionEngine
{
    private readonly IReadOnlyList<Segment> _segments;
    private readonly string _detectedLanguage;

    public FakeTranscriptionEngine(IReadOnlyList<Segment> segments, string detectedLanguage)
    {
        _segments = segments;
        _detectedLanguage = detectedLanguage;
    }

    public Task<TranscriptionResult> TranscribeAsync(
        string audioPath,
        string? sourceLanguage,
        string modelSize,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report(100);
        return Task.FromResult(new TranscriptionResult
        {
            Segments = _segments,
            DetectedLanguage = _detectedLanguage
        });
    }
}

public class FakeTranslationService : ITranslationService
{
    public List<string> RequestedLanguages { get; } = new();

    public Task<IReadOnlyList<Segment>> TranslateAsync(
        IReadOnlyList<Segment> segments,
        string targetLanguage,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        RequestedLanguages.Add(targetLanguage);

        IReadOnlyList<Segment> translated = segments
            .Select(s => new Segment
            {
                Index = s.Index,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Text = s.Text,
                TranslatedText = $"[{targetLanguage}] {s.Text}",
                Language = targetLanguage
            })
            .ToList();

        progress?.Report(100);
        return Task.FromResult(translated);
    }
}

public class FakeVideoBurner : IVideoBurner
{
    public List<(string VideoPath, string SubtitlePath, string OutputPath)> Calls { get; } = new();

    public Task<string> BurnSubtitlesAsync(
        string videoPath,
        string subtitlePath,
        string outputPath,
        CancellationToken ct = default)
    {
        Calls.Add((videoPath, subtitlePath, outputPath));
        File.WriteAllText(outputPath, "fake-burned-video");
        return Task.FromResult(outputPath);
    }
}

public class FakeFileStorage : IFileStorage
{
    private readonly string _basePath;

    public FakeFileStorage(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    public Task<string> SaveVideoAsync(Stream stream, string fileName, CancellationToken ct = default)
        => throw new NotSupportedException("Not needed for pipeline tests.");

    public string GetVideoPath(string jobId) => Path.Combine(_basePath, jobId, "video.mp4");

    public string GetOutputDirectory(string jobId)
    {
        var dir = Path.Combine(_basePath, jobId, "output");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public Stream OpenVideoStream(string jobId) => throw new NotSupportedException("Not needed for pipeline tests.");

    public long GetVideoSize(string jobId) => 0;
}
