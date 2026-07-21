using Microsoft.AspNetCore.Mvc;
using NATS.Client.Core;
using VideoSubtitleTranslator.Core.Enums;
using VideoSubtitleTranslator.Api.Services;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Core.Models;
using VideoSubtitleTranslator.Infrastructure.Progress;
using System.Diagnostics;

namespace VideoSubtitleTranslator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideoController : ControllerBase
{
    private readonly IFileStorage _storage;
    private readonly IJobPublisher _publisher;
    private readonly DirectVideoProcessor _directProcessor;
    private readonly QueueRuntimeState _queueState;
    private readonly QueueInfrastructureBootstrapper _queueBootstrapper;
    private readonly JobProgressStateStore _progressStateStore;

    public VideoController(
        IFileStorage storage,
        IJobPublisher publisher,
        DirectVideoProcessor directProcessor,
        QueueRuntimeState queueState,
        QueueInfrastructureBootstrapper queueBootstrapper,
        JobProgressStateStore progressStateStore)
    {
        _storage = storage;
        _publisher = publisher;
        _directProcessor = directProcessor;
        _queueState = queueState;
        _queueBootstrapper = queueBootstrapper;
        _progressStateStore = progressStateStore;
    }

    public sealed class LocalProcessRequest
    {
        public string VideoPath { get; set; } = string.Empty;
        public string? SourceLanguage { get; set; }
        public string TargetLanguages { get; set; } = "en";
        public string ModelSize { get; set; } = "medium";
        public bool BurnSubtitles { get; set; }
        public string ProcessingMode { get; set; } = "auto";
    }

    private static string NormalizeProcessingMode(string? value)
    {
        var mode = value?.Trim().ToLowerInvariant();
        return mode is "direct" or "queue" ? mode : "auto";
    }

    [HttpPost("upload")]
    [RequestSizeLimit(2L * 1024 * 1024 * 1024)] // 2GB max
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string? sourceLanguage,
        [FromForm] string targetLanguages = "en",
        [FromForm] string modelSize = "medium",
        [FromForm] bool burnSubtitles = false,
        [FromForm] string processingMode = "auto",
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

        var outputDirectory = Path.GetFullPath(_storage.GetOutputDirectory(jobId));
        var progressFilePath = Path.Combine(outputDirectory, ".vst-progress.json");
        _progressStateStore.Track(jobId, progressFilePath);

        var job = new JobCreatedEvent
        {
            JobId = jobId,
            VideoPath = _storage.GetVideoPath(jobId),
            Options = options,
            OutputDirectory = outputDirectory,
            ProgressFilePath = progressFilePath
        };

        var requestedMode = NormalizeProcessingMode(processingMode);

        if (requestedMode == "direct")
        {
            _directProcessor.StartProcessingInBackground(job);
            return Ok(new
            {
                jobId,
                status = "processing-direct",
                detail = "Direct mode selected: processing started immediately in API mode."
            });
        }

        if (requestedMode == "queue" && !_queueState.QueueAvailable)
        {
            var queueReady = await _queueBootstrapper.EnsureQueueInfrastructureAsync(ct);
            if (queueReady)
            {
                _queueState.QueueAvailable = true;
            }
        }

        if (requestedMode == "queue" && !_queueState.QueueAvailable)
        {
            _directProcessor.StartQueuedFallbackInBackground(job);
            return Ok(new
            {
                jobId,
                status = "queued",
                detail = "Queue bootstrap failed: running in local in-process queue fallback mode. Install nats-server or Docker to restore external queue infrastructure."
            });
        }

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
            var queuedProgress = new JobProgressEvent
            {
                JobId = jobId,
                Status = JobStatus.Queued,
                ProgressPercent = 0,
                Stage = "Job queued. Waiting for worker processing..."
            };
            JobProgressFiles.WriteLatest(progressFilePath, queuedProgress);
            await _publisher.PublishProgressAsync(queuedProgress, ct);
        }
        catch (NatsException)
        {
            if (requestedMode == "queue")
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    error = "Queue mode requested but broker submission failed.",
                    detail = "Retry after restoring NATS or switch processing mode to Direct."
                });
            }

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

    [HttpPost("process-local")]
    public async Task<IActionResult> ProcessLocal(
        [FromBody] LocalProcessRequest request,
        CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.VideoPath))
            return BadRequest("VideoPath is required.");

        var path = request.VideoPath.Trim().Trim('"');
        if (!Path.IsPathRooted(path))
            return BadRequest("VideoPath must be an absolute local path.");

        if (!System.IO.File.Exists(path))
            return BadRequest("VideoPath does not exist.");

        var allowedExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".webm" };
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest($"Unsupported format: {ext}");

        var targetLanguages = request.TargetLanguages
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (targetLanguages.Count == 0)
            targetLanguages.Add("en");

        var jobId = Guid.NewGuid().ToString("N");
        var outputDirectory = Path.GetFullPath(Path.GetDirectoryName(path)!);
        var progressFilePath = Path.Combine(outputDirectory, $".vst-progress-{jobId}.json");
        _progressStateStore.Track(jobId, progressFilePath);
        var options = new ProcessingOptions
        {
            SourceLanguage = request.SourceLanguage,
            TargetLanguages = targetLanguages,
            ModelSize = request.ModelSize,
            BurnSubtitles = request.BurnSubtitles
        };

        var job = new JobCreatedEvent
        {
            JobId = jobId,
            VideoPath = path,
            Options = options,
            OutputDirectory = outputDirectory,
            ProgressFilePath = progressFilePath
        };

        var requestedMode = NormalizeProcessingMode(request.ProcessingMode);

        if (requestedMode == "direct")
        {
            _directProcessor.StartProcessingInBackground(job);
            return Ok(new
            {
                jobId,
                status = "processing-direct",
                detail = "Direct mode selected: local path accepted and processing started immediately in API mode."
            });
        }

        if (requestedMode == "queue" && !_queueState.QueueAvailable)
        {
            var queueReady = await _queueBootstrapper.EnsureQueueInfrastructureAsync(ct);
            if (queueReady)
            {
                _queueState.QueueAvailable = true;
            }
        }

        if (requestedMode == "queue" && !_queueState.QueueAvailable)
        {
            _directProcessor.StartQueuedFallbackInBackground(job);
            return Ok(new
            {
                jobId,
                status = "queued",
                detail = "Local path accepted. Queue bootstrap failed: running in local in-process queue fallback mode. Install nats-server or Docker to restore external queue infrastructure."
            });
        }

        if (!_queueState.QueueAvailable)
        {
            _directProcessor.StartProcessingInBackground(job);
            return Ok(new
            {
                jobId,
                status = "processing-direct",
                detail = "Local path accepted. Queue unavailable: running direct processing in API mode."
            });
        }

        try
        {
            await _publisher.PublishJobAsync(job, ct);
            var queuedProgress = new JobProgressEvent
            {
                JobId = jobId,
                Status = JobStatus.Queued,
                ProgressPercent = 0,
                Stage = "Local path accepted. Job queued for worker processing."
            };
            JobProgressFiles.WriteLatest(progressFilePath, queuedProgress);
            await _publisher.PublishProgressAsync(queuedProgress, ct);
            return Ok(new
            {
                jobId,
                status = "queued",
                detail = "Local path accepted. Job queued for worker processing."
            });
        }
        catch (NatsException)
        {
            if (requestedMode == "queue")
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    error = "Queue mode requested but broker submission failed.",
                    detail = "Retry after restoring NATS or switch processing mode to Direct."
                });
            }

            _queueState.QueueAvailable = false;
            _directProcessor.StartProcessingInBackground(job);
            return Ok(new
            {
                jobId,
                status = "processing-direct",
                detail = "Local path accepted. Queue became unavailable: switched to direct processing in API mode."
            });
        }
    }

    [HttpPost("{jobId}/cancel")]
    public IActionResult CancelJob(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return BadRequest("JobId is required.");

        if (_directProcessor.TryCancel(jobId))
        {
            return Ok(new
            {
                jobId,
                status = "cancelling",
                detail = "Cancellation requested. Processing will stop as soon as the current pipeline operation yields."
            });
        }

        return Ok(new
        {
            jobId,
            status = "not-running-locally",
            detail = "This job is not running in the local API process. External worker cancellation is not enabled yet."
        });
    }

    [HttpPost("{jobId}/open-output-folder")]
    public IActionResult OpenOutputFolder(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return BadRequest("JobId is required.");

        var outputDirectory = ResolveOutputDirectory(jobId);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            return NotFound(new { detail = "Output folder is not known for this job yet." });

        Directory.CreateDirectory(outputDirectory);

        try
        {
            OpenFolder(outputDirectory);
            return Ok(new { jobId, outputDirectory, detail = "Output folder opened." });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Unable to open output folder.",
                detail = ex.Message,
                outputDirectory
            });
        }
    }

    private string ResolveOutputDirectory(string jobId)
    {
        if (_progressStateStore.TryGetOutputDirectory(jobId, out var trackedOutputDir) &&
            !string.IsNullOrWhiteSpace(trackedOutputDir))
        {
            return Path.GetFullPath(trackedOutputDir);
        }

        var defaultOutputDir = _storage.GetOutputDirectory(jobId);
        return string.IsNullOrWhiteSpace(defaultOutputDir)
            ? string.Empty
            : Path.GetFullPath(defaultOutputDir);
    }

    private static void OpenFolder(string outputDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = outputDirectory,
                UseShellExecute = true
            });
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = outputDirectory,
                UseShellExecute = false
            });
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "xdg-open",
            Arguments = outputDirectory,
            UseShellExecute = false
        });
    }
}
