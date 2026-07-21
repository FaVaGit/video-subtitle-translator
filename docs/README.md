# Video Subtitle Translator

A modern, cross-platform video subtitle translation application built with **ASP.NET Core 9**, **React 19**, **Fabric.js**, and **Tauri 2.0**.

Automatically extracts audio from video files, transcribes speech using Whisper (ONNX Runtime), translates into 24+ languages, and generates synchronized subtitle files — all with a modern UI and real-time progress tracking.

> Status note: the ONNX Whisper integration is currently scaffolded in infrastructure, but inference logic is still a placeholder (`OnnxWhisperEngine.ProcessChunkAsync`).

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  Client: Browser / Tauri Desktop (WebView)                          │
│  React 19 + Fluent UI + Fabric.js                                   │
│  SSE (EventSource) for real-time progress                           │
└───────────┬─────────────────────────────────────────────────────────┘
            │ REST + SSE
┌───────────▼─────────────────────────────────────────────────────────┐
│  ASP.NET Core 9 API                                                  │
│  Upload → NATS JetStream → Worker                                    │
│  SSE progress endpoint (IAsyncEnumerable)                            │
└───────────┬─────────────────────────────────────────────────────────┘
            │ NATS JetStream
┌───────────▼─────────────────────────────────────────────────────────┐
│  .NET Worker Service                                                 │
│  FFmpeg → Whisper (ONNX) → Google Translate → SRT/VTT Generator     │
└─────────────────────────────────────────────────────────────────────┘
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React 19 + TypeScript + Vite |
| UI Kit | Fluent UI React v9 |
| Canvas | Fabric.js 6.x (subtitle overlay & timeline editor) |
| Realtime | SSE (Server-Sent Events) via `IAsyncEnumerable` |
| Backend | ASP.NET Core 9 |
| Message Queue | NATS JetStream |
| Worker | .NET Worker Service |
| Transcription | ONNX Runtime (Whisper models) |
| Translation | Google Translate API |
| Video Processing | FFmpeg |
| Desktop | Tauri 2.0 (WebView2/WebKit) |
| Container | Docker Compose |

## Project Structure

```
video-subtitle-translator/
├── src/
│   ├── Backend/
│   │   ├── VideoSubtitleTranslator.Api/         # ASP.NET Core Web API
│   │   ├── VideoSubtitleTranslator.Core/        # Domain models & interfaces
│   │   ├── VideoSubtitleTranslator.Infrastructure/ # Implementations
│   │   ├── VideoSubtitleTranslator.Worker/      # Background job processor
│   │   └── VideoSubtitleTranslator.sln
│   ├── Frontend/                                # React SPA
│   │   ├── src/
│   │   │   ├── api/          # HTTP & SSE clients
│   │   │   ├── components/   # UI components
│   │   │   ├── hooks/        # Custom React hooks
│   │   │   ├── store/        # Zustand state management
│   │   │   └── lib/          # Utilities (SRT parser, presets)
│   │   └── package.json
│   └── Desktop/                                 # Tauri 2.0 wrapper
│       └── src-tauri/
├── docker/
│   ├── docker-compose.yml
│   ├── Dockerfile.api
│   ├── Dockerfile.worker
│   └── Dockerfile.frontend
└── README.md
```

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [FFmpeg](https://ffmpeg.org/) on PATH
- [NATS Server](https://nats.io/) (or Docker)
- [Rust](https://www.rust-lang.org/) (for Tauri desktop build only)

### Quick Start (Docker)

```bash
cd docker
docker compose up -d
```

This starts:
- **NATS** on port 4222
- **API** on port 5000
- **Worker** (background processing)
- **Frontend** on port 3000

Open http://localhost:3000

### Quick Start (Scripted launcher)

From repository root:

Windows:

```bat
scripts\run.bat
```

Linux/macOS:

```bash
./scripts/run.sh
```

The launcher auto-detects tools and offers available modes (`dev`, `docker`, `desktop`, `desktop-release`, `api-only`, `frontend-only`).
It also supports `mcp` for the local MCP server session.

### Processing Modes and Runtime Behavior

- `Auto`: uses external queue when available, otherwise falls back to direct API processing.
- `Direct`: starts immediate in-process API execution.
- `Queue`: attempts queue bootstrap (local `nats-server` or Docker `vst-nats`, then worker). If external queue cannot be started, the API accepts the job and runs a local in-process queued fallback.
- The Progress view uses live SSE plus a latest-snapshot fallback endpoint to reduce UI desync when event streaming is interrupted.
- While a job is running, the UI allows:
  - cancellation request,
  - opening the job output folder immediately.

### Development Setup

#### Backend

```bash
cd src/Backend

# Start NATS
docker run -d --name nats -p 4222:4222 -p 8222:8222 nats:2.11-alpine --jetstream

# Run API
dotnet run --project VideoSubtitleTranslator.Api

# Run Worker (separate terminal)
dotnet run --project VideoSubtitleTranslator.Worker
```

#### Frontend

```bash
cd src/Frontend
npm install
npm run dev
```

Opens at http://localhost:5173 with API proxy to localhost:5000.

#### Desktop (Tauri)

```bash
cd src/Desktop
cargo install tauri-cli
cargo tauri dev
```

For packaged desktop execution (no Python required), use:

Windows:

```bat
scripts\run-desktop-release.bat
```

Linux/macOS:

```bash
./scripts/run-desktop-release.sh
```

## Features

- **Video Upload**: Supports MP4, MKV, AVI, MOV, WebM (up to 2GB)
- **Speech Recognition**: Whisper models (tiny → large-v3) via ONNX Runtime
- **Translation**: 24+ languages via Google Translate with rate limiting
- **Subtitle Formats**: SRT and WebVTT output
- **Player**:
  - Player tab is always enabled (aligned with desktop Python UX)
  - Supports direct local video loading from Player tab
  - Processed-job tracks are available when backend output exists
  - HTML5 video with range-based seeking
  - Fabric.js canvas overlay for custom subtitle rendering
  - Panel mode for subtitle display below video
  - Multi-language track switching
  - Customizable styles (font, size, color, position)
  - Keyboard shortcuts
  - Fullscreen support
- **Real-time Progress**: SSE streaming from worker to UI
- **Cross-platform**: Web, Windows, macOS, Linux

Implementation status caveat:
- Speech recognition pipeline wiring exists, but ONNX inference implementation is pending.

## MCP Integration

An MCP server is included in `mcp-server/` to enable direct operations on video files and GitHub-authenticated AI calls.

Available tools:
- `video_probe` (ffprobe metadata)
- `video_extract_frame` (single-frame extraction)
- `video_extract_audio` (mono 16k wav extraction)
- `github_models_chat` (GitHub-authenticated model call via `GITHUB_TOKEN`)

Run scripts:
- Windows: `scripts\run-mcp.bat`
- Linux/macOS: `./scripts/run-mcp.sh`

Authentication note:
- Internal Copilot session tokens are not exposed for external MCP usage.
- Use GitHub authentication (`gh auth login`) and `GITHUB_TOKEN` for model access.

Frontend-first authentication:
- The Transcribe view includes an "MCP & GitHub Authentication" panel.
- From there users can open GitHub token settings, validate token, and save local auth state before starting MCP.

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/video/upload` | Upload video and start processing |
| POST | `/api/video/process-local` | Start a job from a local absolute path without multipart upload |
| POST | `/api/video/{id}/cancel` | Request cancellation for jobs running in local API process |
| POST | `/api/video/{id}/open-output-folder` | Open the resolved output folder on host OS |
| GET | `/api/jobs/{id}/progress` | SSE stream of job progress |
| GET | `/api/jobs/{id}/latest-progress` | Latest persisted progress snapshot fallback |
| GET | `/api/player/stream/{id}` | Stream video (range support) |
| GET | `/api/player/{id}/tracks` | List subtitle tracks |
| GET | `/api/player/{id}/subtitles/{lang}` | Get subtitle cues (JSON/SRT/VTT) |
| GET | `/api/subtitle/{id}` | List all generated subtitle files |
| GET | `/api/subtitle/{id}/download/{file}` | Download subtitle file |

Endpoint naming note:
- The upload endpoint is implemented under `VideoController` route: `/api/video/upload`.

## Configuration

### API (`appsettings.json`)

```json
{
  "Nats": { "Url": "nats://localhost:4222" },
  "Storage": { "BasePath": "./data" }
}
```

### Worker

```json
{
  "Nats": { "Url": "nats://localhost:4222" },
  "Storage": { "BasePath": "./data" },
  "Whisper": { "ModelPath": "./models/whisper-medium" }
}
```

## License

MIT
