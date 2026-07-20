using System.Diagnostics;
using VideoSubtitleTranslator.Core.Interfaces;

namespace VideoSubtitleTranslator.Infrastructure.Video;

/// <summary>
/// Burns a subtitle file into a copy of the source video using ffmpeg,
/// matching the reference Python engine's burn_subtitles behavior
/// (subtitles filter, audio stream copied as-is).
/// </summary>
public class FFmpegSubtitleBurner : IVideoBurner
{
    public async Task<string> BurnSubtitlesAsync(
        string videoPath,
        string subtitlePath,
        string outputPath,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        // ffmpeg's subtitles filter requires forward slashes and escaped colons.
        var escapedSubtitlePath = subtitlePath.Replace("\\", "/").Replace(":", "\\:");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{videoPath}\" -vf \"subtitles='{escapedSubtitlePath}'\" -c:a copy \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        var stderr = new System.Text.StringBuilder();
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
            throw new InvalidOperationException($"FFmpeg subtitle burn failed: {stderr}");
        }

        return outputPath;
    }
}
