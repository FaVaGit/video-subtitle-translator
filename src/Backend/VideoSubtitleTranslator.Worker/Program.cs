using NATS.Client.Core;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Infrastructure.Audio;
using VideoSubtitleTranslator.Infrastructure.Messaging;
using VideoSubtitleTranslator.Infrastructure.Storage;
using VideoSubtitleTranslator.Infrastructure.Subtitle;
using VideoSubtitleTranslator.Infrastructure.Transcription;
using VideoSubtitleTranslator.Infrastructure.Translation;
using VideoSubtitleTranslator.Worker.Consumers;

var builder = Host.CreateApplicationBuilder(args);

// NATS
var natsUrl = builder.Configuration.GetValue<string>("Nats:Url") ?? "nats://localhost:4222";
builder.Services.AddSingleton<INatsConnection>(_ => new NatsConnection(new NatsOpts { Url = natsUrl }));
builder.Services.AddSingleton<NatsJobConsumer>();
builder.Services.AddSingleton<IJobPublisher, NatsJobPublisher>();

// Infrastructure services
builder.Services.AddSingleton<IFileStorage>(_ =>
    new LocalFileStorage(builder.Configuration.GetValue<string>("Storage:BasePath") ?? "./data"));
builder.Services.AddSingleton<IAudioExtractor, FFmpegAudioExtractor>();
builder.Services.AddSingleton<ITranscriptionEngine>(_ =>
    new OnnxWhisperEngine(builder.Configuration.GetValue<string>("Whisper:ModelPath") ?? "./models/whisper-medium"));
builder.Services.AddSingleton<ISubtitleGenerator, SrtGenerator>();
builder.Services.AddHttpClient<ITranslationService, GoogleTranslateService>();

// Worker
builder.Services.AddHostedService<ProcessVideoConsumer>();

var host = builder.Build();
host.Run();
