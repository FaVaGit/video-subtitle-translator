using VideoSubtitleTranslator.Core.Models;
using VideoSubtitleTranslator.Infrastructure.Subtitle;

namespace VideoSubtitleTranslator.Tests;

public class SrtGeneratorTests
{
    private static readonly IReadOnlyList<Segment> Segments = new List<Segment>
    {
        new() { Index = 1, StartTime = 0.0, EndTime = 2.5, Text = "Hello world" },
        new() { Index = 2, StartTime = 2.5, EndTime = 5.125, Text = "Second line", TranslatedText = "Seconda riga" },
    };

    [Fact]
    public async Task GeneratesSrtWithExpectedTimestampFormat()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.srt");
        try
        {
            await new SrtGenerator().GenerateSrtAsync(Segments, path);
            var content = await File.ReadAllTextAsync(path);

            Assert.Contains("00:00:00,000 --> 00:00:02,500", content);
            Assert.Contains("00:00:02,500 --> 00:00:05,125", content);
            Assert.Contains("Hello world", content);
            // Translated text takes priority over original text, matching reference behavior.
            Assert.Contains("Seconda riga", content);
            Assert.DoesNotContain("Second line", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GeneratesVttWithHeaderAndDotSeparatedMilliseconds()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.vtt");
        try
        {
            await new SrtGenerator().GenerateVttAsync(Segments, path);
            var content = await File.ReadAllTextAsync(path);

            Assert.StartsWith("WEBVTT", content);
            Assert.Contains("00:00:00.000 --> 00:00:02.500", content);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
