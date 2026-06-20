"""
Command-line interface for Video Subtitle Translator.

Usage:
    python cli.py "path/to/video.mp4" --languages it fr de
    python cli.py "path/to/video.mp4" --languages it --model large-v3 --device cuda
"""

import argparse
import sys
from engine import SubtitleEngine


def main():
    parser = argparse.ArgumentParser(
        description="Transcribe and translate video subtitles",
    )
    parser.add_argument("video", help="Path to the video file")
    parser.add_argument(
        "-l", "--languages", nargs="+", default=["it", "en"],
        help="Target language codes (default: it en). Available: "
             + ", ".join(c for c in SubtitleEngine.LANGUAGES if c != "auto"),
    )
    parser.add_argument(
        "-s", "--source", default="auto",
        help="Source language code or 'auto' (default: auto)",
    )
    parser.add_argument(
        "-m", "--model", default="medium",
        choices=SubtitleEngine.MODEL_SIZES,
        help="Whisper model size (default: medium)",
    )
    parser.add_argument(
        "-d", "--device", default="auto",
        choices=["auto", "cpu", "cuda"],
        help="Compute device (default: auto)",
    )
    parser.add_argument(
        "-b", "--backend", default="auto",
        choices=["auto", "ctranslate2", "directml"],
        help="Transcription backend (default: auto). "
             "'directml' uses DirectX 12 GPU acceleration (any GPU), "
             "'ctranslate2' uses faster-whisper (CPU/CUDA)",
    )
    parser.add_argument(
        "-o", "--output", default=None,
        help="Output directory (default: same as video)",
    )
    parser.add_argument(
        "--burn", action="store_true",
        help="Burn subtitles into a copy of the video",
    )
    args = parser.parse_args()

    engine = SubtitleEngine(model_size=args.model, device=args.device, backend=args.backend)

    def on_log(msg):
        print(msg)

    def on_progress(frac, stage):
        bar_len = 30
        filled = int(bar_len * frac)
        bar = "█" * filled + "░" * (bar_len - filled)
        print(f"\r  [{bar}] {frac:6.1%}  {stage}", end="", flush=True)
        if frac >= 1.0:
            print()

    try:
        generated = engine.process_video(
            video_path=args.video,
            target_languages=args.languages,
            source_language=args.source,
            output_dir=args.output,
            burn_subs=args.burn,
            on_progress=on_progress,
            on_log=on_log,
        )
        print(f"\nGenerated {len(generated)} file(s):")
        for f in generated:
            print(f"  • {f}")
    except KeyboardInterrupt:
        engine.cancel()
        print("\nCancelled.")
        sys.exit(1)
    except Exception as e:
        print(f"\nError: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
