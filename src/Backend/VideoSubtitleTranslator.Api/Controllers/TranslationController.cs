using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Api.Controllers;

[ApiController]
[Route("api/translation")]
public sealed class TranslationController(ITranslationService translationService) : ControllerBase
{
    private const int MaxTexts = 250;
    private const int MaxCharacters = 100_000;
    private const int MaxSegmentLength = 1_500;
    private static readonly Regex LanguageCodePattern = new("^[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,8})*$", RegexOptions.Compiled);

    public sealed record TextTranslationRequest(string TargetLanguage, IReadOnlyList<string> Texts);
    public sealed record TextTranslationItem(string Text, bool Translated);
    public sealed record TextTranslationResponse(string TargetLanguage, IReadOnlyList<TextTranslationItem> Results);

    [HttpPost("text")]
    [RequestSizeLimit(1_048_576)]
    public async Task<ActionResult<TextTranslationResponse>> TranslateText(
        [FromBody] TextTranslationRequest request,
        CancellationToken cancellationToken)
    {
        if (HttpContext.Connection.RemoteIpAddress is not { } remoteAddress || !IPAddress.IsLoopback(remoteAddress))
            return Forbid();

        var targetLanguage = request?.TargetLanguage;
        if (string.IsNullOrWhiteSpace(targetLanguage) || !LanguageCodePattern.IsMatch(targetLanguage))
            return BadRequest("TargetLanguage must be a language code such as 'fr' or 'zh-CN'.");

        if (request.Texts is null || request.Texts.Count is < 1 or > MaxTexts || request.Texts.Any(text => text is null))
            return BadRequest($"Texts must contain between 1 and {MaxTexts} strings.");

        if (request.Texts.Sum(text => (long)text.Length) > MaxCharacters)
            return BadRequest($"Text content cannot exceed {MaxCharacters} characters per request.");

        var segments = new List<Segment>();
        var ranges = new List<(int Start, int Count)>();
        foreach (var text in request.Texts)
        {
            var start = segments.Count;
            foreach (var chunk in SplitText(text))
            {
                segments.Add(new Segment { Index = segments.Count, Text = chunk });
            }
            ranges.Add((start, segments.Count - start));
        }

        var translated = await translationService.TranslateAsync(segments, targetLanguage, ct: cancellationToken);
        if (translated.Count != segments.Count || translated.Select(segment => segment.Index).Distinct().Count() != segments.Count)
            return StatusCode(StatusCodes.Status502BadGateway, "The translation provider returned an incomplete result.");

        var translatedByIndex = translated.ToDictionary(segment => segment.Index);
        if (translatedByIndex.Keys.Any(index => index < 0 || index >= segments.Count))
            return StatusCode(StatusCodes.Status502BadGateway, "The translation provider returned invalid segment indexes.");

        var results = new List<TextTranslationItem>(request.Texts.Count);
        foreach (var (start, count) in ranges)
        {
            var text = new StringBuilder();
            var succeeded = true;
            for (var index = start; index < start + count; index++)
            {
                var segment = translatedByIndex[index];
                text.Append(segment.TranslatedText ?? segment.Text);
                succeeded &= !segment.TranslationFailed && segment.TranslatedText is not null;
            }
            results.Add(new TextTranslationItem(text.ToString(), succeeded));
        }

        return Ok(new TextTranslationResponse(targetLanguage, results));
    }

    private static IEnumerable<string> SplitText(string text)
    {
        if (text.Length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        for (var start = 0; start < text.Length;)
        {
            var end = Math.Min(start + MaxSegmentLength, text.Length);
            if (end < text.Length)
            {
                var boundary = text.LastIndexOfAny([' ', '\n', '.', '?', '!', ';', ':'], end - 1, end - start);
                if (boundary > start) end = boundary + 1;
                if (end < text.Length && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end])) end--;
            }

            yield return text[start..end];
            start = end;
        }
    }
}