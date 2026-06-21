using System.Globalization;
using System.Text;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Infrastructure.Subtitle;

public class SrtGenerator : ISubtitleGenerator
{
    public async Task<string> GenerateSrtAsync(IReadOnlyList<Segment> segments, string outputPath, CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < segments.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var seg = segments[i];
            var text = seg.TranslatedText ?? seg.Text;

            sb.AppendLine((i + 1).ToString());
            sb.AppendLine($"{FormatTime(seg.StartTime)} --> {FormatTime(seg.EndTime)}");
            sb.AppendLine(text);
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8, ct);
        return outputPath;
    }

    public async Task<string> GenerateVttAsync(IReadOnlyList<Segment> segments, string outputPath, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();

        for (int i = 0; i < segments.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var seg = segments[i];
            var text = seg.TranslatedText ?? seg.Text;

            sb.AppendLine((i + 1).ToString());
            sb.AppendLine($"{FormatTimeVtt(seg.StartTime)} --> {FormatTimeVtt(seg.EndTime)}");
            sb.AppendLine(text);
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8, ct);
        return outputPath;
    }

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.ToString(@"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture);
    }

    private static string FormatTimeVtt(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
    }
}
