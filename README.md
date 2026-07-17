# Video Subtitle Translator

Video subtitle translation platform with web and desktop clients.

Current active stack:
- Backend: ASP.NET Core 9 API + Worker
- Frontend: React 19 + Vite + Fluent UI
- Desktop: Tauri 2 (Rust wrapper over the same frontend)
- Messaging: NATS JetStream

Legacy Python files (`main.py`, `cli.py`, `engine.py`) are still in the repository but are not the primary runtime for the current architecture.

## Current Status

- End-to-end API/frontend/desktop orchestration scripts are available under `scripts/`.
- Desktop can run without Python using Tauri (`run-desktop.bat` / `run-desktop-release.bat`).
- Transcription engine implementation in `OnnxWhisperEngine` is scaffolded, but ONNX inference is still a placeholder.
- Player tab is always accessible (Python-like UX); actual video/subtitle playback is available after a processed job is present.
- Player can load a local video directly from the Player tab (without going through Transcribe first).

## Prerequisites

- .NET 9 SDK
- Node.js 22+
- Rust + Cargo (for desktop modes)
- NATS server (or Docker)
- FFmpeg on PATH

Optional:
- Docker Desktop (for Docker mode or NATS fallback)

## Quick Start (Windows)

```bat
scripts\run.bat
```

Available launcher modes are auto-detected based on installed tools:
- `dev`: API + Worker + Frontend (+ NATS auto-start)
- `docker`: full stack via Docker Compose
- `desktop`: Tauri dev mode + backend (+ NATS)
- `desktop-release`: build and run packaged Tauri binary + backend (+ NATS)
- `api-only`
- `frontend-only`
- `mcp`: MCP server with video tools + GitHub-authenticated AI session

## Quick Start (Linux/macOS)

```bash
./scripts/run.sh
```

Same mode set as Windows.

## Desktop Without Python

To run desktop outside Python:

- Development desktop:
    - `scripts\run-desktop.bat`
- Packaged desktop binary:
    - `scripts\run-desktop-release.bat`

Both flows use .NET + Node + Rust and do not require Python.

## API Surface (Implemented)

- `POST /api/video/upload`
- `GET /api/jobs/{jobId}/progress` (SSE)
- `GET /api/player/stream/{jobId}`
- `GET /api/player/{jobId}/tracks`
- `GET /api/player/{jobId}/subtitles/{lang}`
- `GET /api/subtitle/{jobId}`
- `GET /api/subtitle/{jobId}/download/{fileName}`

## MCP Server (GitHub-authenticated)

The repository includes an MCP server under `mcp-server/` with tools to operate on video content directly:
- `video_probe`
- `video_extract_frame`
- `video_extract_audio`
- `github_models_chat` (uses `GITHUB_TOKEN` for GitHub-authenticated model calls)

Run on Windows:

```bat
scripts\run-mcp.bat
```

Run on Linux/macOS:

```bash
./scripts/run-mcp.sh
```

Authentication note:
- Direct reuse of VS Code Copilot internal auth token is not supported.
- The provided flow uses GitHub authentication via `GITHUB_TOKEN` (automatically imported from `gh auth token` when available).

## Repository Layout

```
video-subtitle-translator/
├── src/
│   ├── Backend/
│   ├── Frontend/
│   └── Desktop/
├── scripts/
├── docker/
├── docs/
├── main.py            # legacy Python GUI
├── cli.py             # legacy Python CLI
└── engine.py          # legacy Python pipeline
```

## Additional Documentation

- Detailed architecture and setup: `docs/README.md`
- ADRs: `docs/ARCHITECTURE.md`

## License

MIT. See `LICENSE`.
