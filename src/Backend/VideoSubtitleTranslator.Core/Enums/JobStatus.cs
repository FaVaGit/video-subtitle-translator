namespace VideoSubtitleTranslator.Core.Enums;

public enum JobStatus
{
    Queued,
    ExtractingAudio,
    Transcribing,
    Translating,
    GeneratingSubtitles,
    BurningSubtitles,
    Completed,
    Failed
}
