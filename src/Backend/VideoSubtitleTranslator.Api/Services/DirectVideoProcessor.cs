using VideoSubtitleTranslator.Core.Enums;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Interfaces;

namespace VideoSubtitleTranslator.Api.Services;

public class DirectVideoProcessor
{
    private readonly IAudioExtractor _audioExtractor;
    private readonly ITranscriptionEngine _transcriptionEngine;
    private readonly ITranslationService _translationService;
    private readonly ISubtitleGenerator _subtitleGenerator;
    private readonly IFileStorage _storage;
    private readonly IProgressBroadcaster _broadcaster;
    private readonly ILogger<DirectVideoProcessor> _logger;

    public DirectVideoProcessor(
        IAudioExtractor audioExtractor,
        ITranscriptionEngine transcriptionEngine,
        ITranslationService translationService,
        ISubtitleGenerator subtitleGenerator,
        IFileStorage storage,
        IProgressBroadcaster broadcaster,
        ILogger<DirectVideoProcessor> logger)
    {
        _audioExtractor = audioExtractor;
        _transcriptionEngine = transcriptionEngine;
        _translationService = translationService;
        _subtitleGenerator = subtitleGenerator;
        _storage = storage;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public void StartProcessingInBackground(JobCreatedEvent job)
    {
        _ = Task.Run(() => ProcessJobAsync(job, CancellationToken.None));
    }

    private async Task ProcessJobAsync(JobCreatedEvent job, CancellationToken ct)
    {
        try
        {
            await ReportProgress(job.JobId, JobStatus.Queued, 0, "Queued (direct processing mode)...", ct);

            var outputDir = _storage.GetOutputDirectory(job.JobId);

            await ReportProgress(job.JobId, JobStatus.ExtractingAudio, 0, "Extracting audio...", ct);
            var audioPath = await _audioExtractor.ExtractAudioAsync(job.VideoPath, outputDir, ct);
            await ReportProgress(job.JobId, JobStatus.ExtractingAudio, 100, "Audio extracted", ct);

            await ReportProgress(job.JobId, JobStatus.Transcribing, 0, "Transcribing...", ct);
            var transcribeProgress = new Progress<int>(p =>
                ReportProgress(job.JobId, JobStatus.Transcribing, p, "Transcribing...", ct).GetAwaiter().GetResult());
            var segments = await _transcriptionEngine.TranscribeAsync(audioPath, job.Options.SourceLanguage, transcribeProgress, ct);
            await ReportProgress(job.JobId, JobStatus.Transcribing, 100, "Transcription complete", ct);

            var totalLangs = Math.Max(job.Options.TargetLanguages.Count, 1);
            for (int i = 0; i < job.Options.TargetLanguages.Count; i++)
            {
                var lang = job.Options.TargetLanguages[i];
                var stagePercent = (i * 100) / totalLangs;
                await ReportProgress(job.JobId, JobStatus.Translating, stagePercent, $"Translating to {lang}...", ct);

                var translated = await _translationService.TranslateAsync(segments, lang, null, ct);

                var srtPath = Path.Combine(outputDir, $"subtitles.{lang}.srt");
                await _subtitleGenerator.GenerateSrtAsync(translated, srtPath, ct);

                var vttPath = Path.Combine(outputDir, $"subtitles.{lang}.vtt");
                await _subtitleGenerator.GenerateVttAsync(translated, vttPath, ct);
            }

            await ReportProgress(job.JobId, JobStatus.Translating, 100, "Translation complete", ct);
            await ReportProgress(job.JobId, JobStatus.Completed, 100, "Processing complete", ct);
            _logger.LogInformation("Direct processing completed for job {JobId}", job.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Direct processing failed for job {JobId}", job.JobId);
            await ReportProgress(job.JobId, JobStatus.Failed, 0, ex.Message, ct);
        }
    }

    private Task ReportProgress(string jobId, JobStatus status, int percent, string stage, CancellationToken ct)
    {
        return _broadcaster.BroadcastAsync(new JobProgressEvent
        {
            JobId = jobId,
            Status = status,
            ProgressPercent = percent,
            Stage = stage
        }, ct);
    }
}
