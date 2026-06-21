using System.Net.Http.Json;
using System.Text.Json;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Infrastructure.Translation;

public class GoogleTranslateService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private const int BatchSize = 20;
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromMilliseconds(250);

    public GoogleTranslateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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

        return translated;
    }

    private async Task<List<string>> TranslateBatchAsync(
        List<string> texts, string targetLanguage, CancellationToken ct)
    {
        // Using Google Translate free endpoint (same as deep-translator)
        var results = new List<string>();

        foreach (var text in texts)
        {
            var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={Uri.EscapeDataString(targetLanguage)}&dt=t&q={Uri.EscapeDataString(text)}";

            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var translated = json[0][0][0].GetString() ?? text;
            results.Add(translated);
        }

        return results;
    }
}
