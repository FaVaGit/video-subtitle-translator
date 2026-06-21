using Microsoft.AspNetCore.Mvc;
using VideoSubtitleTranslator.Core.Interfaces;

namespace VideoSubtitleTranslator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubtitleController : ControllerBase
{
    private readonly IFileStorage _storage;

    public SubtitleController(IFileStorage storage)
    {
        _storage = storage;
    }

    [HttpGet("{jobId}")]
    public IActionResult GetSubtitles(string jobId)
    {
        var outputDir = _storage.GetOutputDirectory(jobId);
        if (!Directory.Exists(outputDir))
            return NotFound();

        var files = Directory.GetFiles(outputDir, "*.srt")
            .Concat(Directory.GetFiles(outputDir, "*.vtt"))
            .Select(f => new
            {
                fileName = Path.GetFileName(f),
                language = Path.GetFileNameWithoutExtension(f).Split('.').Last(),
                format = Path.GetExtension(f).TrimStart('.'),
                downloadUrl = $"/api/subtitle/{jobId}/download/{Path.GetFileName(f)}"
            })
            .ToList();

        return Ok(files);
    }

    [HttpGet("{jobId}/download/{fileName}")]
    public IActionResult Download(string jobId, string fileName)
    {
        // Validate fileName to prevent path traversal
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
            return BadRequest("Invalid file name");

        var outputDir = _storage.GetOutputDirectory(jobId);
        var filePath = Path.Combine(outputDir, fileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var contentType = Path.GetExtension(filePath) switch
        {
            ".srt" => "application/x-subrip",
            ".vtt" => "text/vtt",
            _ => "application/octet-stream"
        };

        return PhysicalFile(filePath, contentType, fileName);
    }
}
