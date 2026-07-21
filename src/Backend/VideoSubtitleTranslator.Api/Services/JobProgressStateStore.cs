namespace VideoSubtitleTranslator.Api.Services;

public class JobProgressStateStore
{
    private sealed record JobPaths(string ProgressFilePath, string OutputDirectory);

    private readonly Dictionary<string, JobPaths> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public void Track(string jobId, string progressFilePath, string outputDirectory)
    {
        lock (_lock)
        {
            _paths[jobId] = new JobPaths(progressFilePath, outputDirectory);
        }
    }

    public bool TryGetPath(string jobId, out string? progressFilePath)
    {
        lock (_lock)
        {
            if (_paths.TryGetValue(jobId, out var paths))
            {
                progressFilePath = paths.ProgressFilePath;
                return true;
            }

            progressFilePath = null;
            return false;
        }
    }

    public bool TryGetOutputDirectory(string jobId, out string? outputDirectory)
    {
        lock (_lock)
        {
            if (_paths.TryGetValue(jobId, out var paths) && !string.IsNullOrWhiteSpace(paths.OutputDirectory))
            {
                outputDirectory = paths.OutputDirectory;
                return true;
            }

            outputDirectory = null;
            return false;
        }
    }
}
