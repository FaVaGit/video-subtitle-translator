using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using VideoSubtitleTranslator.Core.Events;

namespace VideoSubtitleTranslator.Infrastructure.Messaging;

public class NatsJobConsumer
{
    private readonly INatsConnection _nats;
    private readonly INatsJSContext _js;

    public NatsJobConsumer(INatsConnection nats)
    {
        _nats = nats;
        _js = new NatsJSContext((NatsConnection)nats);
    }

    public async IAsyncEnumerable<JobCreatedEvent> ConsumeJobsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var consumer = await _js.CreateOrUpdateConsumerAsync("JOBS", new ConsumerConfig
        {
            DurableName = "workers",
            FilterSubject = "jobs.process.*",
            AckPolicy = ConsumerConfigAckPolicy.Explicit
        }, ct);

        await foreach (var msg in consumer.ConsumeAsync<byte[]>(cancellationToken: ct))
        {
            if (msg.Data is null) continue;

            var job = JsonSerializer.Deserialize<JobCreatedEvent>(msg.Data);
            if (job is not null)
            {
                yield return job;
                await msg.AckAsync(cancellationToken: ct);
            }
        }
    }
}
