using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using NATS.Client.Core;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Interfaces;

namespace VideoSubtitleTranslator.Infrastructure.Progress;

public class SseProgressBroadcaster : IProgressBroadcaster, IDisposable
{
    private readonly INatsConnection _nats;
    private readonly Dictionary<string, Channel<JobProgressEvent>> _channels = new();
    private readonly object _lock = new();

#pragma warning disable CS0649
    private IDisposable? _subscription;
#pragma warning restore CS0649

    public SseProgressBroadcaster(INatsConnection nats)
    {
        _nats = nats;
        _ = StartListeningAsync();
    }

    private async Task StartListeningAsync()
    {
        await foreach (var msg in _nats.SubscribeAsync<byte[]>("jobs.progress.*"))
        {
            if (msg.Data is null) continue;

            var progress = JsonSerializer.Deserialize<JobProgressEvent>(msg.Data);
            if (progress is null) continue;

            lock (_lock)
            {
                if (_channels.TryGetValue(progress.JobId, out var channel))
                {
                    channel.Writer.TryWrite(progress);

                    if (progress.Status == Core.Enums.JobStatus.Completed ||
                        progress.Status == Core.Enums.JobStatus.Failed)
                    {
                        channel.Writer.Complete();
                        _channels.Remove(progress.JobId);
                    }
                }
            }
        }
    }

    public Task BroadcastAsync(JobProgressEvent progress, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_channels.TryGetValue(progress.JobId, out var channel))
            {
                channel.Writer.TryWrite(progress);
            }
        }
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<JobProgressEvent> SubscribeAsync(
        string jobId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<JobProgressEvent>();

        lock (_lock)
        {
            _channels[jobId] = channel;
        }

        await foreach (var evt in channel.Reader.ReadAllAsync(ct))
        {
            yield return evt;
        }
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        lock (_lock)
        {
            foreach (var ch in _channels.Values)
                ch.Writer.TryComplete();
            _channels.Clear();
        }
    }
}
