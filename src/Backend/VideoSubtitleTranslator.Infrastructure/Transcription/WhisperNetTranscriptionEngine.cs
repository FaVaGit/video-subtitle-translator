using Whisper.net;
using Whisper.net.Ggml;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Infrastructure.Transcription;

/// <summary>
/// Real speech-to-text engine using Whisper.net (whisper.cpp bindings).
/// Cross-platform (Windows/Linux/macOS), fully offline after the model is
/// downloaded once, matching the reference Python engine's faster-whisper
/// behavior: auto-detect language, forced source language, per-segment
/// timestamps and text.
/// </summary>
public class WhisperNetTranscriptionEngine : ITranscriptionEngine
{
    private readonly string _modelsDirectory;
    private readonly string _defaultModelSize;
    private static readonly SemaphoreSlim ModelDownloadLock = new(1, 1);

    public WhisperNetTranscriptionEngine(string modelsDirectory, string defaultModelSize)
    {
        _modelsDirectory = modelsDirectory;
        _defaultModelSize = string.IsNullOrWhiteSpace(defaultModelSize) ? "medium" : defaultModelSize;
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        string audioPath,
        string? sourceLanguage,
        string modelSize,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var effectiveModelSize = string.IsNullOrWhiteSpace(modelSize) ? _defaultModelSize : modelSize;
        var modelPath = await EnsureModelAsync(effectiveModelSize, ct);

        using var factory = WhisperFactory.FromPath(modelPath);
        var builder = factory.CreateBuilder();

        var forcedLanguage = string.IsNullOrWhiteSpace(sourceLanguage) || sourceLanguage == "auto"
            ? null
            : sourceLanguage;

        builder = forcedLanguage is not null
            ? builder.WithLanguage(forcedLanguage)
            : builder.WithLanguageDetection();

        await using var processor = builder.Build();

        await using var audioStream = File.OpenRead(audioPath);
        var totalDuration = GetWavDuration(audioPath);

        var segments = new List<Segment>();
        string? detectedLanguage = forcedLanguage;

        await foreach (var result in processor.ProcessAsync(audioStream, ct))
        {
            ct.ThrowIfCancellationRequested();

            var text = result.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(result.Language))
                detectedLanguage ??= result.Language;

            if (text.Length == 0)
                continue;

            segments.Add(new Segment
            {
                Index = segments.Count + 1,
                StartTime = result.Start.TotalSeconds,
                EndTime = result.End.TotalSeconds,
                Text = text
            });

            if (progress is not null && totalDuration > 0)
            {
                var pct = (int)Math.Clamp(result.End.TotalSeconds / totalDuration * 100, 0, 100);
                progress.Report(pct);
            }
        }

        progress?.Report(100);

        return new TranscriptionResult
        {
            Segments = segments,
            DetectedLanguage = detectedLanguage ?? "en"
        };
    }

    private async Task<string> EnsureModelAsync(string modelSize, CancellationToken ct)
    {
        Directory.CreateDirectory(_modelsDirectory);
        var ggmlType = MapModelSize(modelSize);
        var fileName = $"ggml-{modelSize}.bin";
        var modelPath = Path.Combine(_modelsDirectory, fileName);

        if (File.Exists(modelPath))
            return modelPath;

        await ModelDownloadLock.WaitAsync(ct);
        try
        {
            if (File.Exists(modelPath))
                return modelPath;

            using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType, cancellationToken: ct);
            await using var fileStream = File.Create(modelPath);
            await modelStream.CopyToAsync(fileStream, ct);
        }
        finally
        {
            ModelDownloadLock.Release();
        }

        return modelPath;
    }

    private static GgmlType MapModelSize(string modelSize) => modelSize.ToLowerInvariant() switch
    {
        "tiny" => GgmlType.Tiny,
        "base" => GgmlType.Base,
        "small" => GgmlType.Small,
        "medium" => GgmlType.Medium,
        "large-v3" => GgmlType.LargeV3,
        _ => GgmlType.Medium
    };

    private static double GetWavDuration(string wavPath)
    {
        try
        {
            using var stream = File.OpenRead(wavPath);
            using var reader = new BinaryReader(stream);
            reader.ReadBytes(24);
            var sampleRate = reader.ReadInt32();
            reader.ReadBytes(16);
            var dataSize = stream.Length - 44;
            if (sampleRate <= 0) return 0;
            return dataSize / 2.0 / sampleRate; // 16-bit mono PCM
        }
        catch
        {
            return 0;
        }
    }
}
