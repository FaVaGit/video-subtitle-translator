using VideoSubtitleTranslator.Core.Enums;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Infrastructure.Processing;

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
        _ = Task.Run(() => ProcessJobAsync(job, CancellationToken.None));
    }

    private async Task ProcessJobAsync(JobCreatedEvent job, CancellationToken ct)
    {
        try
        {
            await ReportProgress(job.JobId, JobStatus.Queued, 0, "Queued (direct processing mode)...", ct);

            Task ReportPipelineProgress(JobStatus status, int percent, string stage, CancellationToken cancellationToken) =>
                ReportProgress(job.JobId, status, percent, stage, cancellationToken);

            await _pipeline.RunAsync(job, ReportPipelineProgress, ct);
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
