using System.Text.Json;
using VideoSubtitleTranslator.Core.Events;

namespace VideoSubtitleTranslator.Infrastructure.Progress;

public static class JobProgressFiles
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static void WriteLatest(string? path, JobProgressEvent progress)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, JsonSerializer.Serialize(progress, JsonOptions));
    }
}
