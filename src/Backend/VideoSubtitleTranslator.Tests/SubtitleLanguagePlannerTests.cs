using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Tests;

public class SubtitleLanguagePlannerTests
{
    [Fact]
    public void ExcludesDetectedLanguageFromTranslationTargets()
    {
        var result = SubtitleLanguagePlanner.GetLanguagesToTranslate("en", new List<string> { "en", "it", "fr" });

        Assert.Equal(new[] { "it", "fr" }, result);
    }

    [Fact]
    public void IsCaseInsensitiveWhenMatchingDetectedLanguage()
    {
        var result = SubtitleLanguagePlanner.GetLanguagesToTranslate("EN", new List<string> { "en", "it" });

        Assert.Equal(new[] { "it" }, result);
    }

    [Fact]
    public void DeduplicatesTargetLanguages()
    {
        var result = SubtitleLanguagePlanner.GetLanguagesToTranslate("en", new List<string> { "it", "IT", "fr" });

        Assert.Equal(new[] { "it", "fr" }, result);
    }

    [Fact]
    public void ReturnsEmptyWhenAllTargetsMatchDetectedLanguage()
    {
        var result = SubtitleLanguagePlanner.GetLanguagesToTranslate("en", new List<string> { "en" });

        Assert.Empty(result);
    }

    [Fact]
    public void ReturnsAllTargetsWhenNoneMatchDetectedLanguage()
    {
        var result = SubtitleLanguagePlanner.GetLanguagesToTranslate("de", new List<string> { "en", "it", "fr" });

        Assert.Equal(new[] { "en", "it", "fr" }, result);
    }
}
