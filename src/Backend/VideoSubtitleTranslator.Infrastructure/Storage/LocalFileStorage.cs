using VideoSubtitleTranslator.Core.Interfaces;

namespace VideoSubtitleTranslator.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;

    public LocalFileStorage(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveVideoAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var jobDir = Path.Combine(_basePath, jobId);
        Directory.CreateDirectory(jobDir);

        var filePath = Path.Combine(jobDir, fileName);
        await using var fileStream = File.Create(filePath);
        await stream.CopyToAsync(fileStream, ct);

        return jobId;
    }

    public string GetVideoPath(string jobId)
    {
        var jobDir = Path.Combine(_basePath, jobId);
        var files = Directory.GetFiles(jobDir, "*.*")
            .Where(f => !f.EndsWith(".wav") && !f.EndsWith(".srt") && !f.EndsWith(".vtt"));
        return files.FirstOrDefault() ?? throw new FileNotFoundException($"Video not found for job {jobId}");
    }

    public string GetOutputDirectory(string jobId)
    {
        var dir = Path.Combine(_basePath, jobId, "output");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public Stream OpenVideoStream(string jobId)
    {
        var path = GetVideoPath(jobId);
        return File.OpenRead(path);
    }

    public long GetVideoSize(string jobId)
    {
        var path = GetVideoPath(jobId);
        return new FileInfo(path).Length;
    }
}
