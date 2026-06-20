# Video Subtitle Translator

A desktop application that extracts audio from video files, transcribes speech using **OpenAI Whisper** (running locally), translates to multiple languages, and generates synchronized **SRT** subtitle files.

![Python 3.10+](https://img.shields.io/badge/python-3.10%2B-blue)
![License MIT](https://img.shields.io/badge/license-MIT-green)
![Platform Windows](https://img.shields.io/badge/platform-Windows-lightgrey)

## Features

- **Local AI transcription** — uses OpenAI Whisper models, no cloud API keys needed
- **GPU acceleration** — auto-detects DirectML (DirectX 12, any modern GPU) or CUDA (NVIDIA)
- **Multi-language translation** — translates subtitles into 24+ languages via Google Translate
- **Synchronized SRT output** — standard subtitle format compatible with VLC, MPC, etc.
- **GUI + CLI** — desktop application with progress tracking, or command-line for batch use
- **Chunk-by-chunk processing** — real-time progress for long videos, with cancel support
- **Auto language detection** — detects source language or accepts manual selection

## Architecture

```
Video (MP4/MKV/AVI/MOV)
    │
    ▼ FFmpeg
Audio (WAV 16kHz mono)
    │
    ▼ Whisper (DirectML GPU or CPU)
Transcription segments with timestamps
    │
    ▼ Google Translate
Translated segments
    │
    ▼ SRT Generator
Subtitle files (.srt) per language
```

## Prerequisites

| Requirement | Notes |
|---|---|
| **Python 3.10+** | [python.org](https://www.python.org/downloads/) |
| **FFmpeg** | Must be on PATH. Install via `winget install FFmpeg` or [ffmpeg.org](https://ffmpeg.org/download.html) |
| **Any GPU** *(optional)* | DirectX 12 compatible GPU enables acceleration (NVIDIA, AMD, Intel) |

## Installation

```powershell
# Clone the repository
git clone https://github.com/FaVaGit/video-subtitle-translator.git
cd video-subtitle-translator

# Create virtual environment
python -m venv .venv
.venv\Scripts\Activate.ps1

# Install dependencies
pip install -r requirements.txt
```

> **Note:** The first run downloads the selected Whisper model (~1.5 GB for `medium`).
> Models are cached in `~/.cache/huggingface/`.

## Usage

### GUI (recommended)

```powershell
python main.py
```

1. Click **Browse** to select a video file
2. Choose the **Whisper model** size (trade-off: speed vs accuracy)
3. Select **source language** (or leave as auto-detect)
4. Check **target languages** for subtitle translation
5. Click **Start Processing**
6. Watch real-time progress: chunk-by-chunk transcription with timer

The application auto-detects the best available hardware:
- 🟢 **GPU** (DirectML or CUDA) — fast processing
- 🟡 **CPU** — works everywhere, slower

### CLI

```powershell
# Basic usage
python cli.py "path/to/video.mp4" --languages it fr de

# Specify model and source language
python cli.py "video.mp4" --languages it en --model large-v3 --source en

# All options
python cli.py --help
```

## Output

For a video named `Session 1 & 2 & 3.mp4`:

```
Subtitles/
├── Session 1 & 2 & 3.en.srt      ← detected/original language
├── Session 1 & 2 & 3.it.srt      ← Italian translation
├── Session 1 & 2 & 3.fr.srt      ← French translation
└── Session 1 & 2 & 3.de.srt      ← German translation
```

## Playing subtitles

### VLC Media Player
1. Open the video in VLC
2. **Subtitle → Add Subtitle File…**
3. Select the `.srt` file for the desired language

> **Tip:** Place the `.srt` file with the same name as the video in the same folder — VLC loads it automatically.

## Whisper Model Comparison

| Model | Download | Accuracy | Speed (CPU) | Speed (GPU) |
|---|---|---|---|---|
| `tiny` | 75 MB | Good | ~10x RT | ~30x RT |
| `base` | 150 MB | Good+ | ~7x RT | ~25x RT |
| `small` | 500 MB | Very good | ~4x RT | ~18x RT |
| `medium` | 1.5 GB | Excellent | ~2x RT | ~12x RT |
| `large-v3` | 3 GB | Best | ~0.5x RT | ~7x RT |

*RT = real-time. E.g., 2x RT means a 1-hour video processes in ~30 minutes.*

## Supported Languages

Auto-detect + manual selection for: English, Italian, French, German, Spanish, Portuguese, Chinese, Japanese, Korean, Arabic, Russian, Hindi, Dutch, Polish, Turkish, Swedish, Danish, Norwegian, Finnish, Greek, Romanian, Hungarian, Czech, Ukrainian.

## Project Structure

```
video-subtitle-translator/
├── main.py              # GUI application (tkinter)
├── cli.py               # Command-line interface
├── engine.py            # Core pipeline: extract → transcribe → translate → SRT
├── requirements.txt     # Python dependencies
├── LICENSE              # MIT License
└── README.md            # This file
```

## Troubleshooting

| Problem | Solution |
|---|---|
| "FFmpeg not found" | Install FFmpeg: `winget install FFmpeg` and restart terminal |
| CUDA/cublas errors | App auto-falls back to DirectML or CPU — no action needed |
| Wrong language detected | Set source language manually in the GUI dropdown |
| Translation rate limits | Built-in rate limiting handles this; large videos may take extra time |
| Large files (>5 GB) | Audio extraction needs temp disk space; ensure enough free space |

## License

MIT — see [LICENSE](LICENSE)
