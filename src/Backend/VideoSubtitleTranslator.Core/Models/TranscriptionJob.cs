using VideoSubtitleTranslator.Core.Enums;

namespace VideoSubtitleTranslator.Core.Models;

public class TranscriptionJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string VideoFileName { get; set; } = string.Empty;
    public string VideoPath { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public int ProgressPercent { get; set; }
    public string? CurrentStage { get; set; }
    public ProcessingOptions Options { get; set; } = new();
    public List<SubtitleFile> OutputFiles { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
