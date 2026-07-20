using NATS.Client.Core;
using VideoSubtitleTranslator.Api.Services;
using VideoSubtitleTranslator.Api.Endpoints;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Infrastructure.Audio;
using VideoSubtitleTranslator.Infrastructure.Messaging;
using VideoSubtitleTranslator.Infrastructure.Progress;
using VideoSubtitleTranslator.Infrastructure.Storage;
using VideoSubtitleTranslator.Infrastructure.Subtitle;
using VideoSubtitleTranslator.Infrastructure.Transcription;
using VideoSubtitleTranslator.Infrastructure.Translation;

var builder = WebApplication.CreateBuilder(args);

// NATS
var natsUrl = builder.Configuration.GetValue<string>("Nats:Url") ?? "nats://localhost:4222";
builder.Services.AddSingleton<INatsConnection>(_ => new NatsConnection(new NatsOpts { Url = natsUrl }));

// Services
builder.Services.AddSingleton<IFileStorage>(_ =>
    new LocalFileStorage(builder.Configuration.GetValue<string>("Storage:BasePath") ?? "./data"));
builder.Services.AddSingleton<IJobPublisher, NatsJobPublisher>();
builder.Services.AddSingleton<IProgressBroadcaster, SseProgressBroadcaster>();
builder.Services.AddSingleton<IAudioExtractor, FFmpegAudioExtractor>();
builder.Services.AddSingleton<ITranscriptionEngine>(_ =>
    new OnnxWhisperEngine(builder.Configuration.GetValue<string>("Whisper:ModelPath") ?? "./models/whisper-medium"));
builder.Services.AddSingleton<ISubtitleGenerator, SrtGenerator>();
builder.Services.AddHttpClient<ITranslationService, GoogleTranslateService>();
builder.Services.AddSingleton<QueueRuntimeState>();
builder.Services.AddSingleton<DirectVideoProcessor>();

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for frontend dev
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:1420")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();
var queueState = app.Services.GetRequiredService<QueueRuntimeState>();

// Initialize NATS stream
var publisher = (NatsJobPublisher)app.Services.GetRequiredService<IJobPublisher>();
try
{
    await publisher.EnsureStreamAsync();
    queueState.QueueAvailable = true;
}
catch (NatsException ex)
{
    queueState.QueueAvailable = false;
    app.Logger.LogWarning(ex, "NATS is unavailable. API will start, but job submission is disabled until the broker is reachable.");
}

app.MapGet("/api/health", () => Results.Ok(new
{
    backend = "ok",
    queue = queueState.QueueAvailable ? "available" : "unavailable"
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.MapControllers();
app.MapProgressEndpoints();

app.Run();
