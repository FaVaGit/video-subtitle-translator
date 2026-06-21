using Microsoft.AspNetCore.Mvc;
using VideoSubtitleTranslator.Core.Interfaces;

namespace VideoSubtitleTranslator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayerController : ControllerBase
{
    private readonly IFileStorage _storage;

    public PlayerController(IFileStorage storage)
    {
        _storage = storage;
    }

    [HttpGet("stream/{jobId}")]
    public IActionResult StreamVideo(string jobId)
    {
        var path = _storage.GetVideoPath(jobId);
        if (!System.IO.File.Exists(path))
            return NotFound();

        return PhysicalFile(path, "video/mp4", enableRangeProcessing: true);
    }

    [HttpGet("{jobId}/tracks")]
    public IActionResult GetTracks(string jobId)
    {
        var outputDir = _storage.GetOutputDirectory(jobId);
        var srtFiles = Directory.GetFiles(outputDir, "*.srt");

        var tracks = srtFiles.Select(f =>
        {
            var lang = Path.GetFileNameWithoutExtension(f).Split('.').Last();
            return new { language = lang, label = GetLanguageLabel(lang), url = $"/api/player/{jobId}/subtitles/{lang}" };
        }).ToList();

        return Ok(tracks);
    }

    [HttpGet("{jobId}/subtitles/{lang}")]
    public async Task<IActionResult> GetSubtitleTrack(
        string jobId, string lang, [FromQuery] string format = "json", CancellationToken ct = default)
    {
        var outputDir = _storage.GetOutputDirectory(jobId);
        var srtPath = Directory.GetFiles(outputDir, $"*.{lang}.srt").FirstOrDefault();

        if (srtPath is null || !System.IO.File.Exists(srtPath))
            return NotFound();

        if (format == "srt")
            return PhysicalFile(srtPath, "application/x-subrip");

        if (format == "vtt")
        {
            var vttPath = Path.ChangeExtension(srtPath, ".vtt");
            if (System.IO.File.Exists(vttPath))
                return PhysicalFile(vttPath, "text/vtt");
            return NotFound("VTT not generated");
        }

        // Default: JSON cues
        var content = await System.IO.File.ReadAllTextAsync(srtPath, ct);
        var cues = ParseSrtToCues(content);
        return Ok(cues);
    }

    private static List<object> ParseSrtToCues(string srtContent)
    {
        var cues = new List<object>();
        var blocks = srtContent.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3) continue;

            var timeParts = lines[1].Split(" --> ");
            if (timeParts.Length != 2) continue;

            cues.Add(new
            {
                index = int.TryParse(lines[0].Trim(), out var idx) ? idx : 0,
                start = ParseSrtTime(timeParts[0].Trim()),
                end = ParseSrtTime(timeParts[1].Trim()),
                text = string.Join(" ", lines.Skip(2))
            });
        }

        return cues;
    }

    private static double ParseSrtTime(string time)
    {
        // Format: 00:01:23,456
        var parts = time.Replace(',', '.').Split(':');
        if (parts.Length != 3) return 0;

        return double.Parse(parts[0]) * 3600 +
               double.Parse(parts[1]) * 60 +
               double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string GetLanguageLabel(string code) => code switch
    {
        "en" => "English",
        "it" => "Italiano",
        "fr" => "Français",
        "de" => "Deutsch",
        "es" => "Español",
        "pt" => "Português",
        "ja" => "日本語",
        "zh" => "中文",
        "ko" => "한국어",
        "ru" => "Русский",
        "ar" => "العربية",
        _ => code.ToUpperInvariant()
    };
}
