using System.Diagnostics;
using VideoSubtitleTranslator.Core.Interfaces;

namespace VideoSubtitleTranslator.Infrastructure.Audio;

public class FFmpegAudioExtractor : IAudioExtractor
{
    public async Task<string> ExtractAudioAsync(string videoPath, string outputDir, CancellationToken ct = default)
    {
        var outputPath = Path.Combine(outputDir, "audio.wav");
        Directory.CreateDirectory(outputDir);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{videoPath}\" -vn -acodec pcm_s16le -ar 16000 -ac 1 -y \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"FFmpeg failed: {error}");
        }

        return outputPath;
    }
}
