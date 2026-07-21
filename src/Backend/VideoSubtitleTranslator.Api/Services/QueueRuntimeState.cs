namespace VideoSubtitleTranslator.Api.Services;

public class QueueRuntimeState
{
    public string Status { get; set; } = "available";

    public bool QueueAvailable
    {
        get => Status == "available";
        set => Status = value ? "available" : "unavailable";
    }
}
