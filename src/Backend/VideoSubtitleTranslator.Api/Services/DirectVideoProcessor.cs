using VideoSubtitleTranslator.Core.Enums;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Infrastructure.Progress;
using VideoSubtitleTranslator.Infrastructure.Processing;
using System.Collections.Concurrent;

namespace VideoSubtitleTranslator.Api.Services;

/// <summary>
/// Executes jobs directly inside the API process when the message queue
/// (NATS) is unavailable, using the exact same pipeline as the Worker so
/// behavior never diverges between queue mode and direct mode.
/// </summary>
public class DirectVideoProcessor
{
    private readonly VideoProcessingPipeline _pipeline;
    private readonly IProgressBroadcaster _broadcaster;
    private readonly ILogger<DirectVideoProcessor> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobCancellations = new(StringComparer.OrdinalIgnoreCase);

    public DirectVideoProcessor(
        VideoProcessingPipeline pipeline,
        IProgressBroadcaster broadcaster,
        ILogger<DirectVideoProcessor> logger)
    {
        _pipeline = pipeline;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public void StartProcessingInBackground(JobCreatedEvent job)
    {
        StartBackgroundJob(job, "Queued (direct processing mode)...");
    }

    public void StartQueuedFallbackInBackground(JobCreatedEvent job)
    {
        StartBackgroundJob(job, "Queued in local in-process fallback mode...");
    }

    public bool TryCancel(string jobId)
    {
        if (_jobCancellations.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            return true;
        }

        return false;
    }

    private void StartBackgroundJob(JobCreatedEvent job, string initialStage)
    {
        var cts = new CancellationTokenSource();
        if (!_jobCancellations.TryAdd(job.JobId, cts))
        {
            _logger.LogWarning("Job {JobId} is already running in direct processor.", job.JobId);
            cts.Dispose();
            return;
        }

        _ = Task.Run(() => ProcessJobAsync(job, initialStage, cts.Token));
    }

    private async Task ProcessJobAsync(JobCreatedEvent job, string initialStage, CancellationToken ct)
    {
        try
        {
            await ReportProgress(job.JobId, job.ProgressFilePath, JobStatus.Queued, 0, initialStage, ct);

            Task ReportPipelineProgress(JobStatus status, int percent, string stage, CancellationToken cancellationToken) =>
                ReportProgress(job.JobId, job.ProgressFilePath, status, percent, stage, cancellationToken);

            await _pipeline.RunAsync(job, ReportPipelineProgress, ct);
            _logger.LogInformation("Direct processing completed for job {JobId}", job.JobId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Direct processing cancelled for job {JobId}", job.JobId);
            await ReportProgress(job.JobId, null, JobStatus.Failed, 0, "Processing cancelled by user.", CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Direct processing failed for job {JobId}", job.JobId);
            await ReportProgress(job.JobId, null, JobStatus.Failed, 0, ex.Message, CancellationToken.None);
        }
        finally
        {
            if (_jobCancellations.TryRemove(job.JobId, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    private Task ReportProgress(string jobId, string? progressFilePath, JobStatus status, int percent, string stage, CancellationToken ct)
    {
        var progress = new JobProgressEvent
        {
            JobId = jobId,
            Status = status,
            ProgressPercent = percent,
            Stage = stage
        };

        JobProgressFiles.WriteLatest(progressFilePath, progress);
        return _broadcaster.BroadcastAsync(progress, ct);
    }
}
