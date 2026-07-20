namespace VideoSubtitleTranslator.Core.Models;

/// <summary>
/// Decides which subtitle languages require translation versus reusing the
/// original transcript, matching the reference Python engine behavior:
/// the detected/source language transcript is always saved as-is, and only
/// target languages that differ from the detected language are translated.
/// </summary>
public static class SubtitleLanguagePlanner
{
    public static IReadOnlyList<string> GetLanguagesToTranslate(
        string detectedLanguage,
        IReadOnlyList<string> targetLanguages)
    {
        return targetLanguages
            .Where(lang => !string.Equals(lang, detectedLanguage, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
