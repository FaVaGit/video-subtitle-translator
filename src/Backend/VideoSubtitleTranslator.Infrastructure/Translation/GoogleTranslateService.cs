using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Infrastructure.Translation;

public class GoogleTranslateService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleTranslateService> _logger;
    private const int BatchSize = 20;
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromMilliseconds(250);
    private const int MaxAttemptsPerSegment = 4;
    private const int MaxBodyPreviewLength = 220;
    private const int MaxTextPreviewLength = 90;

    public GoogleTranslateService(HttpClient httpClient, ILogger<GoogleTranslateService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Segment>> TranslateAsync(
        IReadOnlyList<Segment> segments,
        string targetLanguage,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var translated = new List<Segment>(segments.Count);
        int totalBatches = (int)Math.Ceiling((double)segments.Count / BatchSize);
        int batchIndex = 0;

        _logger.LogInformation(
            "Translation started: {SegmentCount} segments to '{TargetLanguage}' in {TotalBatches} batches.",
            segments.Count,
            targetLanguage,
            totalBatches);

        foreach (var batch in segments.Chunk(BatchSize))
        {
            ct.ThrowIfCancellationRequested();

            var texts = batch.Select(s => s.Text).ToList();
            var translatedTexts = await TranslateBatchAsync(texts, targetLanguage, ct);

            for (int i = 0; i < batch.Length; i++)
            {
                translated.Add(new Segment
                {
                    Index = batch[i].Index,
                    StartTime = batch[i].StartTime,
                    EndTime = batch[i].EndTime,
                    Text = batch[i].Text,
                    TranslatedText = translatedTexts[i],
                    Language = targetLanguage
                });
            }

            batchIndex++;
            progress?.Report(batchIndex * 100 / totalBatches);

            await Task.Delay(RateLimitDelay, ct);
        }

        _logger.LogInformation(
            "Translation completed: {SegmentCount} segments to '{TargetLanguage}'.",
            translated.Count,
            targetLanguage);

        return translated;
    }

    private async Task<List<string>> TranslateBatchAsync(
        List<string> texts, string targetLanguage, CancellationToken ct)
    {
        var results = new List<string>();
        var fallbackCount = 0;

        for (var i = 0; i < texts.Count; i++)
        {
            var text = texts[i];
            var translated = await TryTranslateWithRetryAsync(text, targetLanguage, ct);
            if (translated is null)
            {
                // Fail-open at segment level so one transient provider error does
                // not fail the whole processing pipeline.
                translated = text;
                fallbackCount++;

                _logger.LogWarning(
                    "Translation fallback applied for segment {SegmentIndex}/{TotalSegments} to '{TargetLanguage}'. Source preview: {TextPreview}",
                    i + 1,
                    texts.Count,
                    targetLanguage,
                    Truncate(text, MaxTextPreviewLength));
            }

            results.Add(translated);
        }

        if (fallbackCount > 0)
        {
            _logger.LogWarning(
                "Translation fallback used for {FallbackCount}/{TotalCount} segments to '{TargetLanguage}'.",
                fallbackCount,
                texts.Count,
                targetLanguage);
        }

        return results;
    }

    private async Task<string?> TryTranslateWithRetryAsync(string text, string targetLanguage, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        for (var attempt = 1; attempt <= MaxAttemptsPerSegment; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={Uri.EscapeDataString(targetLanguage)}&dt=t&q={Uri.EscapeDataString(text)}";

                using var response = await _httpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning(
                        "Translate HTTP error attempt {Attempt}/{MaxAttempts} target '{TargetLanguage}': status {StatusCode} {ReasonPhrase}; body {BodyPreview}; text {TextPreview}",
                        attempt,
                        MaxAttemptsPerSegment,
                        targetLanguage,
                        (int)response.StatusCode,
                        response.ReasonPhrase,
                        Truncate(body, MaxBodyPreviewLength),
                        Truncate(text, MaxTextPreviewLength));

                    if (attempt < MaxAttemptsPerSegment)
                    {
                        var backoffMs = 300 * attempt;
                        await Task.Delay(TimeSpan.FromMilliseconds(backoffMs), ct);
                    }

                    continue;
                }

                var raw = await response.Content.ReadAsStringAsync(ct);
                using var document = JsonDocument.Parse(raw);
                var translated = ParseTranslatedText(document.RootElement, text);
                if (!string.IsNullOrWhiteSpace(translated))
                {
                    return translated;
                }

                _logger.LogWarning(
                    "Translate parse issue attempt {Attempt}/{MaxAttempts} target '{TargetLanguage}': empty translated text; body {BodyPreview}; text {TextPreview}",
                    attempt,
                    MaxAttemptsPerSegment,
                    targetLanguage,
                    Truncate(raw, MaxBodyPreviewLength),
                    Truncate(text, MaxTextPreviewLength));
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                _logger.LogWarning(
                    ex,
                    "Translate attempt {Attempt}/{MaxAttempts} failed for target '{TargetLanguage}'. Text {TextPreview}",
                    attempt,
                    MaxAttemptsPerSegment,
                    targetLanguage,
                    Truncate(text, MaxTextPreviewLength));

                if (attempt < MaxAttemptsPerSegment)
                {
                    var backoffMs = 300 * attempt;
                    await Task.Delay(TimeSpan.FromMilliseconds(backoffMs), ct);
                }
            }
        }

        return null;
    }

    private static string ParseTranslatedText(JsonElement json, string fallback)
    {
        // Expected shape for this endpoint:
        // [ [ ["translated part", "source part", ...], ... ], ... ]
        if (json.ValueKind != JsonValueKind.Array || json.GetArrayLength() == 0)
            return fallback;

        var first = json[0];
        if (first.ValueKind != JsonValueKind.Array)
            return fallback;

        var sb = new StringBuilder();
        foreach (var token in first.EnumerateArray())
        {
            if (token.ValueKind != JsonValueKind.Array || token.GetArrayLength() == 0)
                continue;

            var chunk = token[0].GetString();
            if (!string.IsNullOrWhiteSpace(chunk))
                sb.Append(chunk);
        }

        var translated = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(translated) ? fallback : translated;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= maxLength) return value;
        return value[..maxLength] + "...";
    }
}
