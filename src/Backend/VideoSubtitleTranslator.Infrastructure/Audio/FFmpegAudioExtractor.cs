using System.Diagnostics;
using System.Text;
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
            },
            EnableRaisingEvents = true
        };

        // ffmpeg writes a lot of diagnostic output to stderr; these streams must
        // be drained asynchronously as they are produced, otherwise the OS pipe
        // buffer fills up and ffmpeg blocks on write, hanging the process forever.
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var errorText = stderr.ToString();
            if (errorText.Contains("does not contain any stream", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The video file has no audio track, so speech cannot be transcribed.");
            }

            throw new InvalidOperationException($"FFmpeg failed: {errorText[^Math.Min(500, errorText.Length)..]}");
        }

        return outputPath;
    }
}

