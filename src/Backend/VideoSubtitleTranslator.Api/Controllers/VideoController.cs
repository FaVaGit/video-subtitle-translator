using Microsoft.AspNetCore.Mvc;
using NATS.Client.Core;
using VideoSubtitleTranslator.Api.Services;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideoController : ControllerBase
{
    private readonly IFileStorage _storage;
    private readonly IJobPublisher _publisher;
    private readonly DirectVideoProcessor _directProcessor;
    private readonly QueueRuntimeState _queueState;

    public VideoController(
        IFileStorage storage,
        IJobPublisher publisher,
        DirectVideoProcessor directProcessor,
        QueueRuntimeState queueState)
    {
        _storage = storage;
        _publisher = publisher;
        _directProcessor = directProcessor;
        _queueState = queueState;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(2L * 1024 * 1024 * 1024)] // 2GB max
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string? sourceLanguage,
        [FromForm] string targetLanguages = "en",
        [FromForm] string modelSize = "medium",
        [FromForm] bool burnSubtitles = false,
        CancellationToken ct = default)
    {
        if (file.Length == 0)
            return BadRequest("No file uploaded");

        var allowedExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".webm" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest($"Unsupported format: {ext}");

        await using var stream = file.OpenReadStream();
        var jobId = await _storage.SaveVideoAsync(stream, file.FileName, ct);

        var options = new ProcessingOptions
        {
            SourceLanguage = sourceLanguage,
            TargetLanguages = targetLanguages.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            ModelSize = modelSize,
            BurnSubtitles = burnSubtitles
        };

        var job = new JobCreatedEvent
        {
            JobId = jobId,
            VideoPath = _storage.GetVideoPath(jobId),
            Options = options
        };

        if (!_queueState.QueueAvailable)
        {
            _directProcessor.StartProcessingInBackground(job);
            return Ok(new
            {
                jobId,
                status = "processing-direct",
                detail = "Queue unavailable: running direct processing in API mode."
            });
        }

        try
        {
            await _publisher.PublishJobAsync(job, ct);
        }
        catch (NatsException)
        {
            _queueState.QueueAvailable = false;
            _directProcessor.StartProcessingInBackground(job);
            return Ok(new
            {
                jobId,
                status = "processing-direct",
                detail = "Queue became unavailable: switched to direct processing in API mode."
            });
        }

        return Ok(new { jobId, status = "queued" });
    }
}
