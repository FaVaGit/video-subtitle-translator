using VideoSubtitleTranslator.Core.Enums;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Infrastructure.Messaging;

namespace VideoSubtitleTranslator.Worker.Consumers;

public class ProcessVideoConsumer : BackgroundService
{
    private readonly NatsJobConsumer _consumer;
    private readonly IAudioExtractor _audioExtractor;
    private readonly ITranscriptionEngine _transcriptionEngine;
    private readonly ITranslationService _translationService;
    private readonly ISubtitleGenerator _subtitleGenerator;
    private readonly IJobPublisher _publisher;
    private readonly IFileStorage _storage;
    private readonly ILogger<ProcessVideoConsumer> _logger;

    public ProcessVideoConsumer(
        NatsJobConsumer consumer,
        IAudioExtractor audioExtractor,
        ITranscriptionEngine transcriptionEngine,
        ITranslationService translationService,
        ISubtitleGenerator subtitleGenerator,
        IJobPublisher publisher,
        IFileStorage storage,
        ILogger<ProcessVideoConsumer> logger)
    {
        _consumer = consumer;
        _audioExtractor = audioExtractor;
        _transcriptionEngine = transcriptionEngine;
        _translationService = translationService;
        _subtitleGenerator = subtitleGenerator;
        _publisher = publisher;
        _storage = storage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started, waiting for jobs...");

        await foreach (var job in _consumer.ConsumeJobsAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process job {JobId}", job.JobId);
                await _publisher.PublishProgressAsync(new JobProgressEvent
                {
                    JobId = job.JobId,
                    Status = JobStatus.Failed,
                    Stage = ex.Message,
                    ProgressPercent = 0
                }, stoppingToken);
            }
        }
    }

    private async Task ProcessJobAsync(JobCreatedEvent job, CancellationToken ct)
    {
        var outputDir = _storage.GetOutputDirectory(job.JobId);

        // Stage 1: Extract audio
        await ReportProgress(job.JobId, JobStatus.ExtractingAudio, 0, "Extracting audio...");
        var audioPath = await _audioExtractor.ExtractAudioAsync(job.VideoPath, outputDir, ct);
        await ReportProgress(job.JobId, JobStatus.ExtractingAudio, 100, "Audio extracted");

        // Stage 2: Transcribe
        await ReportProgress(job.JobId, JobStatus.Transcribing, 0, "Transcribing...");
        var transcribeProgress = new Progress<int>(p =>
            ReportProgress(job.JobId, JobStatus.Transcribing, p, "Transcribing...").GetAwaiter().GetResult());
        var segments = await _transcriptionEngine.TranscribeAsync(audioPath, job.Options.SourceLanguage, transcribeProgress, ct);
        await ReportProgress(job.JobId, JobStatus.Transcribing, 100, "Transcription complete");

        // Stage 3: Translate to each target language
        var totalLangs = job.Options.TargetLanguages.Count;
        for (int i = 0; i < totalLangs; i++)
        {
            var lang = job.Options.TargetLanguages[i];
            var stagePercent = (i * 100) / totalLangs;
            await ReportProgress(job.JobId, JobStatus.Translating, stagePercent, $"Translating to {lang}...");

            var translated = await _translationService.TranslateAsync(segments, lang, null, ct);

            // Generate SRT
            var srtPath = Path.Combine(outputDir, $"subtitles.{lang}.srt");
            await _subtitleGenerator.GenerateSrtAsync(translated, srtPath, ct);

            // Generate VTT
            var vttPath = Path.Combine(outputDir, $"subtitles.{lang}.vtt");
            await _subtitleGenerator.GenerateVttAsync(translated, vttPath, ct);
        }

        await ReportProgress(job.JobId, JobStatus.Translating, 100, "Translation complete");

        // Stage 4: Done
        await ReportProgress(job.JobId, JobStatus.Completed, 100, "Processing complete");
        _logger.LogInformation("Job {JobId} completed successfully", job.JobId);
    }

    private Task ReportProgress(string jobId, JobStatus status, int percent, string stage)
    {
        return _publisher.PublishProgressAsync(new JobProgressEvent
        {
            JobId = jobId,
            Status = status,
            ProgressPercent = percent,
            Stage = stage
        });
    }
}
