using VideoSubtitleTranslator.Core.Enums;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Models;
using VideoSubtitleTranslator.Infrastructure.Processing;
using VideoSubtitleTranslator.Infrastructure.Subtitle;
using VideoSubtitleTranslator.Tests.TestDoubles;

namespace VideoSubtitleTranslator.Tests;

public class VideoProcessingPipelineTests
{
    private static readonly List<Segment> SampleSegments = new()
    {
        new Segment { Index = 1, StartTime = 0, EndTime = 2, Text = "Hello there" },
        new Segment { Index = 2, StartTime = 2, EndTime = 4, Text = "General greetings" },
    };

    private sealed record ProgressEvent(JobStatus Status, int Percent, string Stage);

    [Fact]
    public async Task SavesOriginalLanguageAndTranslatesOnlyOtherTargets()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"pipeline-test-{Guid.NewGuid():N}");
        var storage = new FakeFileStorage(tempRoot);
        var translation = new FakeTranslationService();
        var pipeline = new VideoProcessingPipeline(
            new FakeAudioExtractor(),
            new FakeTranscriptionEngine(SampleSegments, "en"),
            translation,
            new SrtGenerator(),
            new FakeVideoBurner(),
            storage);

        var job = new JobCreatedEvent
        {
            JobId = "job-1",
            VideoPath = "C:/videos/sample.mp4",
            Options = new ProcessingOptions
            {
                TargetLanguages = new List<string> { "en", "it", "fr" },
                ModelSize = "tiny",
                BurnSubtitles = false
            }
        };

        var events = new List<ProgressEvent>();
        Task Report(JobStatus status, int percent, string stage, CancellationToken ct)
        {
            events.Add(new ProgressEvent(status, percent, stage));
            return Task.CompletedTask;
        }

        try
        {
            await pipeline.RunAsync(job, Report);

            var outputDir = storage.GetOutputDirectory(job.JobId);

            Assert.True(File.Exists(Path.Combine(outputDir, "subtitles.en.srt")));
            Assert.True(File.Exists(Path.Combine(outputDir, "subtitles.en.vtt")));
            Assert.True(File.Exists(Path.Combine(outputDir, "subtitles.it.srt")));
            Assert.True(File.Exists(Path.Combine(outputDir, "subtitles.fr.srt")));

            // Only languages different from the detected one are translated.
            Assert.Equal(new[] { "it", "fr" }, translation.RequestedLanguages);

            Assert.Contains(events, e => e.Status == JobStatus.ExtractingAudio);
            Assert.Contains(events, e => e.Status == JobStatus.Transcribing);
            Assert.Contains(events, e => e.Status == JobStatus.Translating);
            Assert.Equal(JobStatus.Completed, events.Last().Status);

            // Matches reference engine: the intermediate extracted audio is
            // deleted once processing finishes, it is not a deliverable.
            Assert.Empty(Directory.GetFiles(outputDir, "audio.wav"));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task BurnsSubtitlesWhenRequested()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"pipeline-test-{Guid.NewGuid():N}");
        var storage = new FakeFileStorage(tempRoot);
        var burner = new FakeVideoBurner();
        var pipeline = new VideoProcessingPipeline(
            new FakeAudioExtractor(),
            new FakeTranscriptionEngine(SampleSegments, "en"),
            new FakeTranslationService(),
            new SrtGenerator(),
            burner,
            storage);

        var job = new JobCreatedEvent
        {
            JobId = "job-2",
            VideoPath = "C:/videos/sample.mp4",
            Options = new ProcessingOptions
            {
                TargetLanguages = new List<string> { "en", "it" },
                ModelSize = "tiny",
                BurnSubtitles = true
            }
        };

        try
        {
            await pipeline.RunAsync(job, (_, _, _, _) => Task.CompletedTask);

            Assert.Single(burner.Calls);
            Assert.EndsWith("subtitles.it.srt", burner.Calls[0].SubtitlePath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DoesNotBurnWhenNoTranslationWasNeeded()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"pipeline-test-{Guid.NewGuid():N}");
        var storage = new FakeFileStorage(tempRoot);
        var burner = new FakeVideoBurner();
        var pipeline = new VideoProcessingPipeline(
            new FakeAudioExtractor(),
            new FakeTranscriptionEngine(SampleSegments, "en"),
            new FakeTranslationService(),
            new SrtGenerator(),
            burner,
            storage);

        // Only target language matches the detected language, so nothing is
        // translated and the reference engine behavior is to skip burning.
        var job = new JobCreatedEvent
        {
            JobId = "job-2b",
            VideoPath = "C:/videos/sample.mp4",
            Options = new ProcessingOptions
            {
                TargetLanguages = new List<string> { "en" },
                ModelSize = "tiny",
                BurnSubtitles = true
            }
        };

        try
        {
            await pipeline.RunAsync(job, (_, _, _, _) => Task.CompletedTask);

            Assert.Empty(burner.Calls);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ReportsCompletedWithNoOutputWhenNoSpeechDetected()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"pipeline-test-{Guid.NewGuid():N}");
        var storage = new FakeFileStorage(tempRoot);
        var pipeline = new VideoProcessingPipeline(
            new FakeAudioExtractor(),
            new FakeTranscriptionEngine(new List<Segment>(), "en"),
            new FakeTranslationService(),
            new SrtGenerator(),
            new FakeVideoBurner(),
            storage);

        var job = new JobCreatedEvent
        {
            JobId = "job-3",
            VideoPath = "C:/videos/silent.mp4",
            Options = new ProcessingOptions { TargetLanguages = new List<string> { "en" } }
        };

        var events = new List<ProgressEvent>();
        Task Report(JobStatus status, int percent, string stage, CancellationToken ct)
        {
            events.Add(new ProgressEvent(status, percent, stage));
            return Task.CompletedTask;
        }

        try
        {
            await pipeline.RunAsync(job, Report);

            Assert.Equal(JobStatus.Completed, events.Last().Status);
            Assert.Contains("No speech detected", events.Last().Stage);

            var outputDir = storage.GetOutputDirectory(job.JobId);
            Assert.Empty(Directory.GetFiles(outputDir, "subtitles.*"));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
