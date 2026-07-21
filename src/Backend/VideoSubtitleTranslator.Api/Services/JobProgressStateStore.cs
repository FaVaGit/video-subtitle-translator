namespace VideoSubtitleTranslator.Api.Services;

public class JobProgressStateStore
{
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public void Track(string jobId, string progressFilePath)
    {
        lock (_lock)
        {
            _paths[jobId] = progressFilePath;
        }
    }

    public bool TryGetPath(string jobId, out string? progressFilePath)
    {
        lock (_lock)
        {
            return _paths.TryGetValue(jobId, out progressFilePath);
        }
    }

    public bool TryGetOutputDirectory(string jobId, out string? outputDirectory)
    {
        outputDirectory = null;

        if (!TryGetPath(jobId, out var progressFilePath) || string.IsNullOrWhiteSpace(progressFilePath))
            return false;

        outputDirectory = Path.GetDirectoryName(progressFilePath);
        return !string.IsNullOrWhiteSpace(outputDirectory);
    }
}
