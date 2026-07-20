using VideoSubtitleTranslator.Core.Enums;
using VideoSubtitleTranslator.Core.Events;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Core.Models;

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
        var outputDir = _storage.GetOutputDirectory(job.JobId);
        string? audioPath = null;

        try
        {
            await reportProgress(JobStatus.ExtractingAudio, 0, "Extracting audio...", ct);
            audioPath = await _audioExtractor.ExtractAudioAsync(job.VideoPath, outputDir, ct);
            await reportProgress(JobStatus.ExtractingAudio, 100, "Audio extracted", ct);

            await reportProgress(JobStatus.Transcribing, 0, "Transcribing...", ct);
            var transcribeProgress = new SynchronousProgress<int>(p =>
                reportProgress(JobStatus.Transcribing, p, "Transcribing...", ct).GetAwaiter().GetResult());
            var transcription = await _transcriptionEngine.TranscribeAsync(
                audioPath, job.Options.SourceLanguage, job.Options.ModelSize, transcribeProgress, ct);

            if (transcription.Segments.Count == 0)
            {
                await reportProgress(JobStatus.Completed, 100, "No speech detected in the audio.", ct);
                return;
            }

            var detectedLanguage = transcription.DetectedLanguage;
            await reportProgress(JobStatus.Transcribing, 100, $"Transcription complete (detected: {detectedLanguage})", ct);

            // Original-language transcript is always saved as-is (matches reference engine).
            await _subtitleGenerator.GenerateSrtAsync(
                transcription.Segments, Path.Combine(outputDir, $"subtitles.{detectedLanguage}.srt"), ct);
            await _subtitleGenerator.GenerateVttAsync(
                transcription.Segments, Path.Combine(outputDir, $"subtitles.{detectedLanguage}.vtt"), ct);

            var languagesToTranslate = SubtitleLanguagePlanner.GetLanguagesToTranslate(
                detectedLanguage, job.Options.TargetLanguages);
            var totalLangs = Math.Max(languagesToTranslate.Count, 1);
            var lastTranslatedLanguage = detectedLanguage;

            for (int i = 0; i < languagesToTranslate.Count; i++)
            {
                var lang = languagesToTranslate[i];
                var stagePercent = (i * 100) / totalLangs;
                await reportProgress(JobStatus.Translating, stagePercent, $"Translating to {lang}...", ct);

                var translated = await _translationService.TranslateAsync(transcription.Segments, lang, null, ct);

                await _subtitleGenerator.GenerateSrtAsync(
                    translated, Path.Combine(outputDir, $"subtitles.{lang}.srt"), ct);
                await _subtitleGenerator.GenerateVttAsync(
                    translated, Path.Combine(outputDir, $"subtitles.{lang}.vtt"), ct);

                lastTranslatedLanguage = lang;
            }

            await reportProgress(JobStatus.Translating, 100, "Translation complete", ct);

            // Matches reference engine: only burn when at least one translated
            // subtitle was produced in addition to the original transcript, and
            // burn the last one generated (the last requested target language).
            if (job.Options.BurnSubtitles && languagesToTranslate.Count > 0)
            {
                await reportProgress(JobStatus.BurningSubtitles, 0, "Burning subtitles into video...", ct);

                var burnSrtPath = Path.Combine(outputDir, $"subtitles.{lastTranslatedLanguage}.srt");
                var burnedVideoPath = Path.Combine(outputDir, $"burned{Path.GetExtension(job.VideoPath)}");
                await _videoBurner.BurnSubtitlesAsync(job.VideoPath, burnSrtPath, burnedVideoPath, ct);

                await reportProgress(JobStatus.BurningSubtitles, 100, "Subtitles burned into video copy", ct);
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
        }
    }
}
