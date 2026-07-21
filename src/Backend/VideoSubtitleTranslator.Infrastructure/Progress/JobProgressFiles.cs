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

        var payload = JsonSerializer.Serialize(progress, JsonOptions);
        ExecuteWithRetry(() =>
        {
            using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream);
            writer.Write(payload);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        });
    }

    public static bool TryReadLatest(string? path, out string json)
    {
        json = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            return false;

        try
        {
            string content = string.Empty;
            ExecuteWithRetry(() =>
            {
                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                content = reader.ReadToEnd();
            });
            json = content;
            return !string.IsNullOrWhiteSpace(json);
        }
        catch
        {
            return false;
        }
    }

    private static void ExecuteWithRetry(Action action, int maxAttempts = 8)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException ex)
            {
                lastError = ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                lastError = ex;
            }

            if (attempt < maxAttempts)
            {
                Thread.Sleep(40 * attempt);
            }
        }

        throw new IOException("Unable to access progress file after retries.", lastError);
    }
}
