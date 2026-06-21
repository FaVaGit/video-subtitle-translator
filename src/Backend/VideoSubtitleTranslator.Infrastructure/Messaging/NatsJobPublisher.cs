using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Interfaces;

namespace VideoSubtitleTranslator.Infrastructure.Messaging;

public class NatsJobPublisher : IJobPublisher
{
    private readonly INatsConnection _nats;
    private readonly INatsJSContext _js;

    public NatsJobPublisher(INatsConnection nats)
    {
        _nats = nats;
        _js = new NatsJSContext((NatsConnection)nats);
    }

    public async Task EnsureStreamAsync(CancellationToken ct = default)
    {
        await _js.CreateStreamAsync(new StreamConfig("JOBS", ["jobs.>"]), ct);
    }

    public async Task PublishJobAsync(JobCreatedEvent job, CancellationToken ct = default)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(job);
        await _js.PublishAsync($"jobs.process.{job.JobId}", data, cancellationToken: ct);
    }

    public async Task PublishProgressAsync(JobProgressEvent progress, CancellationToken ct = default)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(progress);
        await _nats.PublishAsync($"jobs.progress.{progress.JobId}", data, cancellationToken: ct);
    }
}
