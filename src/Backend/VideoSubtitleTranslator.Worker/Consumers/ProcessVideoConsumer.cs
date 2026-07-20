using VideoSubtitleTranslator.Core.Enums;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Infrastructure.Messaging;
using VideoSubtitleTranslator.Infrastructure.Processing;

namespace VideoSubtitleTranslator.Worker.Consumers;

public class ProcessVideoConsumer : BackgroundService
{
    private readonly NatsJobConsumer _consumer;
    private readonly VideoProcessingPipeline _pipeline;
    private readonly IJobPublisher _publisher;
    private readonly ILogger<ProcessVideoConsumer> _logger;

    public ProcessVideoConsumer(
        NatsJobConsumer consumer,
        VideoProcessingPipeline pipeline,
        IJobPublisher publisher,
        ILogger<ProcessVideoConsumer> logger)
    {
        _consumer = consumer;
        _pipeline = pipeline;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started, waiting for jobs...");

        await foreach (var job in _consumer.ConsumeJobsAsync(stoppingToken))
        {
            try
            {
                Task ReportProgress(JobStatus status, int percent, string stage, CancellationToken ct) =>
                    _publisher.PublishProgressAsync(new JobProgressEvent
                    {
                        JobId = job.JobId,
                        Status = status,
                        ProgressPercent = percent,
                        Stage = stage
                    }, ct);

                await _pipeline.RunAsync(job, ReportProgress, stoppingToken);
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
}

