namespace VideoSubtitleTranslator.Core.Models;

public class SubtitleFile
{
    public string Language { get; set; } = string.Empty;
    public string LanguageLabel { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Format { get; set; } = "srt";
}
