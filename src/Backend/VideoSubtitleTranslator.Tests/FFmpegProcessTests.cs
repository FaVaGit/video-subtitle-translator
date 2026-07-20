using System.Diagnostics;
using VideoSubtitleTranslator.Core.Models;
using VideoSubtitleTranslator.Infrastructure.Audio;
using VideoSubtitleTranslator.Infrastructure.Subtitle;
using VideoSubtitleTranslator.Infrastructure.Video;

namespace VideoSubtitleTranslator.Tests;

/// <summary>
/// Regression tests for the ffmpeg process invocations. These exercise the
/// real ffmpeg binary (required to be on PATH) against a synthetic video
/// with verbose console output, specifically to catch the class of deadlock
/// where redirected stdout/stderr pipes are never drained and ffmpeg blocks
/// forever on write() once the OS pipe buffer fills up.
/// </summary>
public class FFmpegProcessTests
{
    private static readonly TimeSpan HangTimeout = TimeSpan.FromSeconds(30);

    private static bool IsFfmpegAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("ffmpeg", "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> CreateSyntheticVideoAsync(string outputPath)
    {
        // Verbose libx264 stats + sine tone audio produce enough stderr output
        // to fill a small OS pipe buffer if it is not drained continuously.
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -f lavfi -i testsrc=duration=3:size=640x480:rate=25 " +
                            $"-f lavfi -i sine=frequency=440:duration=3 " +
                            $"-c:v libx264 -preset ultrafast -c:a aac \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdoutTask, stderrTask);

        if (process.ExitCode != 0 || !File.Exists(outputPath))
            throw new InvalidOperationException("Failed to create synthetic test video with ffmpeg.");

        return outputPath;
    }

    [SkippableFact]
    public async Task AudioExtractorDoesNotHangAndProducesNonEmptyOutput()
    {
        Skip.IfNot(IsFfmpegAvailable(), "ffmpeg is not available on PATH in this environment.");

        var tempDir = Path.Combine(Path.GetTempPath(), $"ffmpeg-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var videoPath = await CreateSyntheticVideoAsync(Path.Combine(tempDir, "source.mp4"));
            var extractor = new FFmpegAudioExtractor();

            var task = extractor.ExtractAudioAsync(videoPath, tempDir);
            var completed = await Task.WhenAny(task, Task.Delay(HangTimeout));

            Assert.True(completed == task, "FFmpeg audio extraction hung (did not complete within timeout).");
            var audioPath = await task;
            Assert.True(new FileInfo(audioPath).Length > 0, "Extracted audio file is empty.");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [SkippableFact]
    public async Task SubtitleBurnerDoesNotHangAndProducesNonEmptyOutput()
    {
        Skip.IfNot(IsFfmpegAvailable(), "ffmpeg is not available on PATH in this environment.");

        var tempDir = Path.Combine(Path.GetTempPath(), $"ffmpeg-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var videoPath = await CreateSyntheticVideoAsync(Path.Combine(tempDir, "source.mp4"));

            var srtPath = Path.Combine(tempDir, "subtitles.srt");
            var segments = new List<Segment>
            {
                new() { Index = 1, StartTime = 0, EndTime = 1.5, Text = "Regression test caption" },
            };
            await new SrtGenerator().GenerateSrtAsync(segments, srtPath);

            var outputPath = Path.Combine(tempDir, "burned.mp4");
            var burner = new FFmpegSubtitleBurner();

            var task = burner.BurnSubtitlesAsync(videoPath, srtPath, outputPath);
            var completed = await Task.WhenAny(task, Task.Delay(HangTimeout));

            Assert.True(completed == task, "FFmpeg subtitle burn hung (did not complete within timeout).");
            await task;
            Assert.True(new FileInfo(outputPath).Length > 0, "Burned video file is empty.");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
