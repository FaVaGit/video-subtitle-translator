using VideoSubtitleTranslator.Core.Enums;

namespace VideoSubtitleTranslator.Core.Models;

public class ProcessingOptions
{
    public string ModelSize { get; set; } = "medium";
    public TranscriptionBackend Backend { get; set; } = TranscriptionBackend.OnnxDirectML;
    public string? SourceLanguage { get; set; }
    public List<string> TargetLanguages { get; set; } = ["en"];
    public bool BurnSubtitles { get; set; }
}
