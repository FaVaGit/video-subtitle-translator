using VideoSubtitleTranslator.Core.Enums;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Core.Models;
using System.Globalization;

namespace VideoSubtitleTranslator.Infrastructure.Processing;

/// <summary>
/// Single shared implementation of the full processing pipeline
/// (extract audio -> transcribe -> save original subtitles -> translate
/// remaining languages -> optionally burn subtitles), used identically by
/// both the queue-based Worker and the API's direct-processing fallback so
/// behavior never diverges between the two execution modes.
/// </summary>
public class VideoProcessingPipeline
{
    private readonly IAudioExtractor _audioExtractor;
    private readonly ITranscriptionEngine _transcriptionEngine;
    private readonly ITranslationService _translationService;
    private readonly ISubtitleGenerator _subtitleGenerator;
    private readonly IVideoBurner _videoBurner;
    private readonly IFileStorage _storage;

    public VideoProcessingPipeline(
        IAudioExtractor audioExtractor,
        ITranscriptionEngine transcriptionEngine,
        ITranslationService translationService,
        ISubtitleGenerator subtitleGenerator,
        IVideoBurner videoBurner,
        IFileStorage storage)
    {
        _audioExtractor = audioExtractor;
        _transcriptionEngine = transcriptionEngine;
        _translationService = translationService;
        _subtitleGenerator = subtitleGenerator;
        _videoBurner = videoBurner;
        _storage = storage;
    }

    public async Task RunAsync(
        JobCreatedEvent job,
        Func<JobStatus, int, string, CancellationToken, Task> reportProgress,
        CancellationToken ct = default)
    {
        var outputDir = string.IsNullOrWhiteSpace(job.OutputDirectory)
            ? _storage.GetOutputDirectory(job.JobId)
            : job.OutputDirectory;
        Directory.CreateDirectory(outputDir);
        var tmpDir = Path.Combine(outputDir, "tmp", job.JobId);
        Directory.CreateDirectory(tmpDir);
        var subtitleBaseName = Path.GetFileNameWithoutExtension(job.VideoPath);
        string? audioPath = null;
        IReadOnlyList<Segment>? sourceSegments = null;
        string? detectedLanguage = null;
        string? existingOriginalSrtPath = null;
        var reusedExistingOriginal = false;

        try
        {
            int Scale(int start, int end, int percent) => start + (int)Math.Round((end - start) * (Math.Clamp(percent, 0, 100) / 100d));

            if (!job.Options.OverwriteOriginalSubtitle &&
                TryLoadExistingOriginalSegments(
                    outputDir,
                    subtitleBaseName,
                    job.Options.SourceLanguage,
                    out var reusedSegments,
                    out var reusedLanguage,
                    out var reusedOriginalPath))
            {
                sourceSegments = reusedSegments;
                detectedLanguage = reusedLanguage;
                existingOriginalSrtPath = reusedOriginalPath;
                reusedExistingOriginal = true;

                await reportProgress(
                    JobStatus.Transcribing,
                    Scale(10, 70, 100),
                    $"Existing original subtitles found ({detectedLanguage}). Reusing them for translation.",
                    ct);
            }
            else
            {
                await reportProgress(JobStatus.ExtractingAudio, Scale(0, 10, 0), "Extracting audio...", ct);
                audioPath = await _audioExtractor.ExtractAudioAsync(job.VideoPath, tmpDir, ct);
                await reportProgress(JobStatus.ExtractingAudio, Scale(0, 10, 100), "Audio extracted", ct);

                await reportProgress(JobStatus.Transcribing, Scale(10, 70, 0), "Transcribing...", ct);
                var transcribeProgress = new SynchronousProgress<int>(p =>
                    reportProgress(JobStatus.Transcribing, Scale(10, 70, p), "Transcribing...", ct).GetAwaiter().GetResult());
                var transcription = await _transcriptionEngine.TranscribeAsync(
                    audioPath, job.Options.SourceLanguage, job.Options.ModelSize, transcribeProgress, ct);

                if (transcription.Segments.Count == 0)
                {
                    await reportProgress(JobStatus.Completed, 100, "No speech detected in the audio.", ct);
                    return;
                }

                sourceSegments = transcription.Segments;
                detectedLanguage = transcription.DetectedLanguage;
                await reportProgress(JobStatus.Transcribing, Scale(10, 70, 100), $"Transcription complete (detected: {detectedLanguage})", ct);
            }

            if (sourceSegments is null || sourceSegments.Count == 0 || string.IsNullOrWhiteSpace(detectedLanguage))
            {
                await reportProgress(JobStatus.Failed, 0, "No subtitle source available for translation.", ct);
                return;
            }

            var languagesToTranslate = SubtitleLanguagePlanner.GetLanguagesToTranslate(
                detectedLanguage, job.Options.TargetLanguages);
            var willBurn = job.Options.BurnSubtitles && languagesToTranslate.Count > 0;
            var generateEnd = languagesToTranslate.Count > 0 || willBurn ? 80 : 98;
            var translateEnd = languagesToTranslate.Count > 0 ? (willBurn ? 95 : 99) : generateEnd;
            var burnEnd = willBurn ? 99 : translateEnd;
            var originalSrtPath = GetSubtitlePath(outputDir, subtitleBaseName, detectedLanguage, "srt");
            var originalVttPath = GetSubtitlePath(outputDir, subtitleBaseName, detectedLanguage, "vtt");

            // Original-language transcript is always saved as-is (matches reference engine).
            await reportProgress(JobStatus.GeneratingSubtitles, Scale(70, generateEnd, 0), $"Generating subtitles for {detectedLanguage}...", ct);

            if (reusedExistingOriginal && !string.IsNullOrWhiteSpace(existingOriginalSrtPath))
            {
                if (!Path.GetFullPath(existingOriginalSrtPath).Equals(Path.GetFullPath(originalSrtPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(existingOriginalSrtPath, originalSrtPath, overwrite: true);
                }
            }
            else
            {
                await _subtitleGenerator.GenerateSrtAsync(sourceSegments, originalSrtPath, ct);
            }

            await _subtitleGenerator.GenerateVttAsync(
                sourceSegments, originalVttPath, ct);
            await reportProgress(JobStatus.GeneratingSubtitles, Scale(70, generateEnd, 100), $"Original subtitles generated ({detectedLanguage})", ct);

            var totalLangs = Math.Max(languagesToTranslate.Count, 1);
            var lastTranslatedLanguage = detectedLanguage;

            for (int i = 0; i < languagesToTranslate.Count; i++)
            {
                var lang = languagesToTranslate[i];
                var stagePercent = (i * 100) / totalLangs;
                await reportProgress(JobStatus.Translating, Scale(generateEnd, translateEnd, stagePercent), $"Translating to {lang}...", ct);

                var translated = await _translationService.TranslateAsync(sourceSegments, lang, null, ct);

                await _subtitleGenerator.GenerateSrtAsync(
                    translated, GetSubtitlePath(outputDir, subtitleBaseName, lang, "srt"), ct);
                await _subtitleGenerator.GenerateVttAsync(
                    translated, GetSubtitlePath(outputDir, subtitleBaseName, lang, "vtt"), ct);

                lastTranslatedLanguage = lang;
                var completedPercent = ((i + 1) * 100) / totalLangs;
                await reportProgress(JobStatus.Translating, Scale(generateEnd, translateEnd, completedPercent), $"Translated subtitles generated for {lang}", ct);
            }

            if (languagesToTranslate.Count > 0)
            {
                await reportProgress(JobStatus.Translating, Scale(generateEnd, translateEnd, 100), "Translation complete", ct);
            }

            // Matches reference engine: only burn when at least one translated
            // subtitle was produced in addition to the original transcript, and
            // burn the last one generated (the last requested target language).
            if (willBurn)
            {
                await reportProgress(JobStatus.BurningSubtitles, Scale(translateEnd, burnEnd, 0), "Burning subtitles into video...", ct);

                var burnSrtPath = GetSubtitlePath(outputDir, subtitleBaseName, lastTranslatedLanguage, "srt");
                var burnedVideoPath = Path.Combine(outputDir, $"burned{Path.GetExtension(job.VideoPath)}");
                await _videoBurner.BurnSubtitlesAsync(job.VideoPath, burnSrtPath, burnedVideoPath, ct);

                await reportProgress(JobStatus.BurningSubtitles, Scale(translateEnd, burnEnd, 100), "Subtitles burned into video copy", ct);
            }

            await reportProgress(JobStatus.Completed, 100, "Processing complete", ct);
        }
        finally
        {
            // Temporary extracted audio is not part of the deliverable output,
            // matching the reference engine's cleanup of the intermediate WAV file.
            if (audioPath is not null)
            {
                try { File.Delete(audioPath); } catch { /* best-effort cleanup */ }
            }

            try
            {
                if (Directory.Exists(tmpDir))
                {
                    Directory.Delete(tmpDir, recursive: true);

                    var tmpRoot = Path.GetDirectoryName(tmpDir);
                    if (!string.IsNullOrWhiteSpace(tmpRoot) &&
                        Directory.Exists(tmpRoot) &&
                        !Directory.EnumerateFileSystemEntries(tmpRoot).Any())
                    {
                        Directory.Delete(tmpRoot, recursive: false);
                    }
                }
            }
            catch
            {
                // best-effort cleanup of transient conversion files/folders
            }
        }
    }

    private static bool TryLoadExistingOriginalSegments(
        string outputDir,
        string subtitleBaseName,
        string? preferredLanguage,
        out IReadOnlyList<Segment> segments,
        out string detectedLanguage,
        out string subtitlePath)
    {
        segments = Array.Empty<Segment>();
        detectedLanguage = string.Empty;
        subtitlePath = string.Empty;

        if (!Directory.Exists(outputDir))
            return false;

        var normalizedPreferredLanguage = NormalizeLanguage(preferredLanguage);
        var candidates = Directory.GetFiles(outputDir, "*.srt")
            .Select(path => new
            {
                Path = path,
                Language = TryExtractLanguage(Path.GetFileName(path), subtitleBaseName)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Language))
            .ToList();

        if (candidates.Count == 0)
            return false;

        var selected = candidates.FirstOrDefault(x =>
            string.Equals(x.Language, normalizedPreferredLanguage, StringComparison.OrdinalIgnoreCase));

        selected ??= candidates.FirstOrDefault(x => string.Equals(x.Language, "en", StringComparison.OrdinalIgnoreCase));
        selected ??= candidates.First();

        var parsed = ParseSrtSegments(selected.Path);
        if (parsed.Count == 0)
            return false;

        segments = parsed;
        detectedLanguage = selected.Language!;
        subtitlePath = selected.Path;
        return true;
    }

    private static string GetSubtitlePath(string outputDir, string subtitleBaseName, string language, string extension)
    {
        return Path.Combine(outputDir, $"{subtitleBaseName}.{language}.{extension}");
    }

    private static string NormalizeLanguage(string? language)
    {
        var normalized = (language ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "" or "auto" ? string.Empty : normalized;
    }

    private static string? TryExtractLanguage(string fileName, string subtitleBaseName)
    {
        if (!fileName.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
            return null;

        if (fileName.StartsWith($"{subtitleBaseName}.", StringComparison.OrdinalIgnoreCase))
        {
            var core = fileName[..^4];
            var parts = core.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? parts[^1].ToLowerInvariant() : null;
        }

        if (fileName.StartsWith("subtitles.", StringComparison.OrdinalIgnoreCase))
        {
            var core = fileName[..^4];
            var parts = core.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? parts[^1].ToLowerInvariant() : null;
        }

        return null;
    }

    private static List<Segment> ParseSrtSegments(string srtPath)
    {
        var content = File.ReadAllText(srtPath);
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var blocks = new List<List<string>>();
        var current = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                if (current.Count > 0)
                {
                    blocks.Add(current);
                    current = new List<string>();
                }

                continue;
            }

            current.Add(line);
        }

        if (current.Count > 0)
            blocks.Add(current);

        var result = new List<Segment>();
        foreach (var block in blocks)
        {
            if (block.Count < 2)
                continue;

            var timeLineIndex = block[0].Contains("-->", StringComparison.Ordinal) ? 0 : 1;
            if (timeLineIndex >= block.Count)
                continue;

            var timeLine = block[timeLineIndex];
            var timeParts = timeLine.Split(" --> ", StringSplitOptions.RemoveEmptyEntries);
            if (timeParts.Length != 2)
                continue;

            if (!TryParseSrtTime(timeParts[0], out var start) || !TryParseSrtTime(timeParts[1], out var end))
                continue;

            var textLines = block.Skip(timeLineIndex + 1).ToArray();
            var text = string.Join("\n", textLines).Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            result.Add(new Segment
            {
                Index = result.Count + 1,
                StartTime = start,
                EndTime = end,
                Text = text
            });
        }

        return result;
    }

    private static bool TryParseSrtTime(string value, out double seconds)
    {
        seconds = 0;
        var normalized = value.Trim().Replace(',', '.');
        var parts = normalized.Split(':');
        if (parts.Length != 3)
            return false;

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var hours))
            return false;
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes))
            return false;
        if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var secs))
            return false;

        seconds = hours * 3600d + minutes * 60d + secs;
        return true;
    }
}
