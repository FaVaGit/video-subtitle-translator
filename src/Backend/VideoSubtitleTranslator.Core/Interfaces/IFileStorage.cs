namespace VideoSubtitleTranslator.Core.Interfaces;

public interface IFileStorage
{
    Task<string> SaveVideoAsync(Stream stream, string fileName, CancellationToken ct = default);
    string GetVideoPath(string jobId);
    string GetOutputDirectory(string jobId);
    Stream OpenVideoStream(string jobId);
    long GetVideoSize(string jobId);
}
