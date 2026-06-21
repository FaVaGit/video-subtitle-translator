# Architecture Decision Records

## ADR-001: SSE over SignalR for Real-time Progress

**Decision**: Use Server-Sent Events (SSE) via ASP.NET Core `IAsyncEnumerable` instead of SignalR.

**Rationale**:
- Progress is unidirectional (server → client), no bidirectional needed
- Native browser support via `EventSource` — zero client-side library
- Simpler infrastructure (no WebSocket negotiation/fallback)
- Works through all proxies and CDNs without special configuration
- Built-in reconnection in `EventSource` API

**Trade-offs**:
- No bidirectional communication (not needed for this use case)
- Max ~6 connections per domain in HTTP/1.1 (not an issue with HTTP/2)

---

## ADR-002: NATS JetStream over RabbitMQ

**Decision**: Use NATS with JetStream for job queuing and progress pub/sub.

**Rationale**:
- Single binary deployment (~20MB), zero external dependencies
- JetStream provides persistence, exactly-once delivery, replay
- `NATS.Net` v2 client is modern, AOT-friendly, high-performance
- 10x faster throughput than RabbitMQ at 1/10 RAM usage
- Built-in key-value store and object store for future use
- Simple configuration vs RabbitMQ's extensive setup

---

## ADR-003: Tauri 2.0 over Electron/MAUI

**Decision**: Use Tauri 2.0 for desktop packaging, wrapping the same React SPA.

**Rationale**:
- Uses system WebView (WebView2 on Windows, WebKit on macOS/Linux)
- ~5-10MB bundle vs ~150MB for Electron
- Rust backend for native operations (file dialog, notifications)
- Same frontend code for web and desktop — no duplication
- Cross-platform: Windows, macOS, Linux from single codebase

---

## ADR-004: Fabric.js for Subtitle Rendering

**Decision**: Use Fabric.js canvas overlay for subtitle display and editing.

**Rationale**:
- Full control over text rendering (font, stroke, shadow, position)
- Interactive mode for subtitle editing (drag, resize, retime)
- Performance: canvas rendering is GPU-accelerated
- Rich object model matches subtitle styling requirements
- Can be extended to timeline editing

---

## ADR-005: ONNX Runtime for Whisper Inference

**Decision**: Use Microsoft.ML.OnnxRuntime.DirectML for Whisper transcription in .NET.

**Rationale**:
- Native .NET — no Python sidecar needed
- DirectML supports any DirectX 12 GPU (AMD, Intel, NVIDIA)
- CUDA provider available for NVIDIA-specific optimization
- Single runtime reduces deployment complexity
- Model can be pre-converted to ONNX format
