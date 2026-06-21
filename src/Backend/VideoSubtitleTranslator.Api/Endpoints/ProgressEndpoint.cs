using System.Runtime.CompilerServices;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Interfaces;

namespace VideoSubtitleTranslator.Api.Endpoints;

public static class ProgressEndpoint
{
    public static void MapProgressEndpoints(this WebApplication app)
    {
        app.MapGet("/api/jobs/{jobId}/progress", async (
            string jobId,
            IProgressBroadcaster broadcaster,
            HttpContext context,
            CancellationToken ct) =>
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";

            await foreach (var evt in broadcaster.SubscribeAsync(jobId, ct))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(evt);
                await context.Response.WriteAsync($"data: {json}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
        });
    }
}
