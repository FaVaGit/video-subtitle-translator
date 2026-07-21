using System.Runtime.CompilerServices;
using System.Text.Json;
using VideoSubtitleTranslator.Api.Services;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Interfaces;

namespace VideoSubtitleTranslator.Api.Endpoints;

public static class ProgressEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void MapProgressEndpoints(this WebApplication app)
    {
        app.MapGet("/api/jobs/{jobId}/latest-progress", (
            string jobId,
            JobProgressStateStore progressStateStore) =>
        {
            if (!progressStateStore.TryGetPath(jobId, out var progressFilePath) ||
                string.IsNullOrWhiteSpace(progressFilePath) ||
                !File.Exists(progressFilePath))
            {
                return Results.NotFound();
            }

            var json = File.ReadAllText(progressFilePath);
            return Results.Content(json, "application/json");
        });

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
                var json = JsonSerializer.Serialize(evt, JsonOptions);
                await context.Response.WriteAsync($"data: {json}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
        });
    }
}
