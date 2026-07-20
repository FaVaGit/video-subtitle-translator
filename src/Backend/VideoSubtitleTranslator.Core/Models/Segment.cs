namespace VideoSubtitleTranslator.Core.Models;

public class Segment
{
    public int Index { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? TranslatedText { get; set; }
    public string Language { get; set; } = string.Empty;
}
