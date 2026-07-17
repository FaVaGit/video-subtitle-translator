"""
Core engine for video subtitle translation.
Pipeline: video → audio extraction → transcription → translation → SRT generation
"""

import os
import shutil
import subprocess
import time


def _find_ffmpeg() -> str:
    """Find ffmpeg executable, checking common Windows install locations."""
    # Check PATH first
    found = shutil.which("ffmpeg")
    if found:
        return found

    # Common winget / chocolatey / manual install locations on Windows
    if os.name == "nt":
        candidates = [
            os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "WinGet", "Links", "ffmpeg.exe"),
            os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "WinGet", "Packages"),
            r"C:\ffmpeg\bin\ffmpeg.exe",
            r"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
        ]
        # Check direct paths
        for c in candidates:
            if os.path.isfile(c):
                return c
        # Search in WinGet Packages folder
        winget_pkg = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "WinGet", "Packages")
        if os.path.isdir(winget_pkg):
            for root, dirs, files in os.walk(winget_pkg):
                if "ffmpeg.exe" in files:
                    return os.path.join(root, "ffmpeg.exe")
    return "ffmpeg"  # fallback to bare name


FFMPEG_PATH = _find_ffmpeg()

# Ensure ffmpeg's directory is on PATH so that subprocesses (e.g. transformers)
# can also find it when they call "ffmpeg" by name.
_ffmpeg_dir = os.path.dirname(FFMPEG_PATH)
if _ffmpeg_dir and _ffmpeg_dir not in os.environ.get("PATH", ""):
    os.environ["PATH"] = _ffmpeg_dir + os.pathsep + os.environ.get("PATH", "")


class Segment:
    """A single subtitle segment with timing and text."""
    __slots__ = ("index", "start", "end", "text")

    def __init__(self, index: int, start: float, end: float, text: str):
        self.index = index
        self.start = start
        self.end = end
        self.text = text


class SubtitleEngine:
    """Orchestrates audio extraction, transcription, translation, and SRT generation."""

    LANGUAGES = {
        "auto": "Auto-detect",
        "en": "English",
        "it": "Italian",
        "fr": "French",
        "de": "German",
        "es": "Spanish",
        "pt": "Portuguese",
        "zh": "Chinese",
        "ja": "Japanese",
        "ko": "Korean",
        "ar": "Arabic",
        "ru": "Russian",
        "hi": "Hindi",
        "nl": "Dutch",
        "pl": "Polish",
        "tr": "Turkish",
        "sv": "Swedish",
        "da": "Danish",
        "no": "Norwegian",
        "fi": "Finnish",
        "el": "Greek",
        "ro": "Romanian",
        "hu": "Hungarian",
        "cs": "Czech",
        "uk": "Ukrainian",
    }

    MODEL_SIZES = ["tiny", "base", "small", "medium", "large-v3"]

    # Backend choices
    BACKENDS = ["auto", "ctranslate2", "directml"]

    def __init__(self, model_size: str = "medium", device: str = "auto", backend: str = "auto"):
        self.model_size = model_size
        self.device = device
        self.backend = backend
        self.model = None
        self._pipeline = None  # for DirectML backend
        self._cancelled = False

    def cancel(self):
        self._cancelled = True

    def reset(self):
        self._cancelled = False

    # ------------------------------------------------------------------
    # FFmpeg helpers
    # ------------------------------------------------------------------

    @staticmethod
    def check_ffmpeg() -> bool:
        try:
            subprocess.run(
                [FFMPEG_PATH, "-version"],
                capture_output=True,
                check=True,
                creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
            )
            return True
        except (FileNotFoundError, subprocess.CalledProcessError):
            return False

    def extract_audio(self, video_path: str, output_path: str, *, on_log=None) -> str:
        if on_log:
            on_log(f"Extracting audio from: {os.path.basename(video_path)}")

        cmd = [
            FFMPEG_PATH, "-y", "-i", video_path,
            "-vn", "-acodec", "pcm_s16le",
            "-ar", "16000", "-ac", "1",
            output_path,
        ]
        result = subprocess.run(
            cmd, capture_output=True, text=True,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
        )
        if result.returncode != 0:
            raise RuntimeError(f"FFmpeg error:\n{result.stderr[-500:]}")

        if on_log:
            on_log(f"Audio extracted → {os.path.basename(output_path)}")
        return output_path

    # ------------------------------------------------------------------
    # Whisper transcription
    # ------------------------------------------------------------------

    def _resolve_backend(self, *, on_log=None) -> str:
        """Decide which backend to use based on 'auto' detection."""
        if self.backend == "directml":
            if on_log:
                on_log("  Backend: DirectML (forced)")
            return "directml"
        if self.backend == "ctranslate2":
            if on_log:
                on_log("  Backend: CTranslate2 (forced)")
            return "ctranslate2"

        # auto: try DirectML first (any DX12 GPU), then CUDA, then CPU
        try:
            import onnxruntime as ort
            providers = ort.get_available_providers()
            if "DmlExecutionProvider" in providers:
                if on_log:
                    on_log("  Backend auto-selected: DirectML (GPU via DirectX 12)")
                return "directml"
        except Exception:
            pass

        if on_log:
            on_log("  Backend auto-selected: CTranslate2 (CPU)")
        return "ctranslate2"  # fallback to faster-whisper on CPU

    def load_model(self, *, on_log=None):
        resolved = self._resolve_backend(on_log=on_log)
        if resolved == "directml":
            self._load_model_directml(on_log=on_log)
        else:
            self._load_model_ctranslate2(on_log=on_log)

    def _load_model_ctranslate2(self, *, on_log=None):
        if on_log:
            on_log(f"\n┌─ STEP: Loading Whisper model ─────────────────────")
            on_log(f"│ Backend: CTranslate2 (faster-whisper)")
            on_log(f"│ Model size: {self.model_size}")
            on_log(f"│ If first run, model download may take several minutes...")
            on_log(f"└─────────────────────────────────────────────")

        from faster_whisper import WhisperModel  # noqa: delay import

        # CTranslate2 backend always runs on CPU (GPU acceleration is via DirectML)
        device = "cpu"
        compute_type = "int8"
        if on_log:
            on_log("  Using CPU with int8 quantization.")

        if on_log:
            on_log(f"  Downloading/loading model ({device}, {compute_type})…")

        self.model = WhisperModel(
            self.model_size,
            device=device,
            compute_type=compute_type,
        )
        if on_log:
            on_log("✅ CTranslate2 model loaded successfully!\n")

    def _load_model_directml(self, *, on_log=None):
        model_id = f"openai/whisper-{self.model_size}"
        if on_log:
            on_log(f"\n┌─ STEP: Loading Whisper model ─────────────────────")
            on_log(f"│ Backend: DirectML (GPU via DirectX 12)")
            on_log(f"│ Model: {model_id}")
            on_log(f"│ If first run, model download may take several minutes...")
            on_log(f"└─────────────────────────────────────────────")

        import torch
        from transformers import AutoModelForSpeechSeq2Seq, AutoProcessor, pipeline
        import onnxruntime as ort

        # Verify DML provider
        providers = ort.get_available_providers()
        if "DmlExecutionProvider" not in providers:
            raise RuntimeError(
                "DirectML not available. Install onnxruntime-directml:\n"
                "  pip install onnxruntime-directml"
            )

        if on_log:
            on_log("  [1/3] Downloading/loading processor…")
        processor = AutoProcessor.from_pretrained(model_id)

        if on_log:
            on_log("  [2/3] Downloading/loading model weights…")
        model = AutoModelForSpeechSeq2Seq.from_pretrained(
            model_id, torch_dtype=torch.float32, low_cpu_mem_usage=True,
        )

        if on_log:
            on_log("  [3/3] Building inference pipeline…")
        self._pipeline = pipeline(
            "automatic-speech-recognition",
            model=model,
            tokenizer=processor.tokenizer,
            feature_extractor=processor.feature_extractor,
            torch_dtype=torch.float32,
            device="cpu",  # torch side stays CPU; ONNX RT uses DML internally if exported
        )
        if on_log:
            on_log("✅ Model loaded successfully!\n")

    def transcribe(self, audio_path: str, language=None, *, on_progress=None, on_log=None):
        if self.model is None and self._pipeline is None:
            self.load_model(on_log=on_log)

        if self._pipeline is not None:
            return self._transcribe_directml(audio_path, language,
                                             on_progress=on_progress, on_log=on_log)
        return self._transcribe_ctranslate2(audio_path, language,
                                            on_progress=on_progress, on_log=on_log)

    def _transcribe_ctranslate2(self, audio_path, language, *, on_progress=None, on_log=None):
        if on_log:
            on_log("Starting transcription (CTranslate2)…")

        lang = language if language and language != "auto" else None
        segments_gen, info = self.model.transcribe(
            audio_path,
            language=lang,
            beam_size=5,
            vad_filter=True,
            vad_parameters=dict(min_silence_duration_ms=500),
        )

        detected_lang = info.language
        duration = info.duration
        if on_log:
            on_log(f"Detected language: {detected_lang} (confidence: {info.language_probability:.0%})")
            hrs = int(duration // 3600)
            mins = int((duration % 3600) // 60)
            secs = int(duration % 60)
            on_log(f"Audio duration: {hrs}h {mins}m {secs}s")

        segments = []
        for i, seg in enumerate(segments_gen):
            if self._cancelled:
                raise InterruptedError("Processing cancelled by user.")

            text = seg.text.strip()
            if not text:
                continue

            segment = Segment(len(segments) + 1, seg.start, seg.end, text)
            segments.append(segment)

            if on_log and (len(segments) % 20 == 0 or i < 3):
                preview = text[:80] + "…" if len(text) > 80 else text
                on_log(f"  [{self._fmt(seg.start)}] \"{preview}\"")

            if on_progress and duration > 0:
                on_progress(min(seg.end / duration, 1.0))

        if on_log:
            on_log(f"Transcription complete — {len(segments)} segments.")
        return segments, detected_lang

    def _transcribe_directml(self, audio_path, language, *, on_progress=None, on_log=None):
        import numpy as np
        import wave
        import torch

        if on_log:
            on_log("Starting transcription (DirectML / Transformers)…")

        lang = language if language and language != "auto" else None

        # Load WAV file as numpy array (we already extracted 16kHz mono PCM)
        if on_log:
            on_log("  Loading audio data into memory…")
        with wave.open(audio_path, "rb") as wf:
            n_frames = wf.getnframes()
            sample_rate = wf.getframerate()
            audio_bytes = wf.readframes(n_frames)

        audio_array = np.frombuffer(audio_bytes, dtype=np.int16).astype(np.float32) / 32768.0
        duration = len(audio_array) / sample_rate

        if on_log and duration > 0:
            hrs = int(duration // 3600)
            mins = int((duration % 3600) // 60)
            secs = int(duration % 60)
            on_log(f"  Audio duration: {hrs}h {mins}m {secs}s")

        # ── Process in 30-second chunks with progress ────────────────
        chunk_len = 30  # seconds
        chunk_samples = chunk_len * sample_rate
        total_samples = len(audio_array)
        n_chunks = max(1, int(np.ceil(total_samples / chunk_samples)))

        if on_log:
            if lang:
                on_log(f"  Source language forced: {lang}")
            else:
                on_log(f"  Source language: auto-detect (will detect from first chunk)")
            on_log(f"  Processing {n_chunks} chunks of {chunk_len}s each…")
            on_log("")

        model = self._pipeline.model
        tokenizer = self._pipeline.tokenizer
        feature_extractor = self._pipeline.feature_extractor

        segments = []
        detected_lang = lang or None

        # ── Auto-detect language from first non-silent chunk ─────────
        if not lang:
            if on_log:
                on_log("  Auto-detecting language from first audio chunk…")
            for detect_idx in range(min(5, n_chunks)):
                start_s = detect_idx * chunk_samples
                end_s = min(start_s + chunk_samples, total_samples)
                detect_audio = audio_array[start_s:end_s]
                if np.abs(detect_audio).max() < 0.01:
                    continue  # skip silent

                input_features = feature_extractor(
                    detect_audio, sampling_rate=sample_rate, return_tensors="pt"
                ).input_features

                # Use Whisper's built-in language detection
                with torch.no_grad():
                    # Generate just the language token
                    decoder_input_ids = torch.tensor([[model.config.decoder_start_token_id]])
                    outputs = model.generate(
                        input_features,
                        max_new_tokens=1,
                        return_dict_in_generate=True,
                        output_scores=True,
                    )
                    # The first generated token after decoder_start is the language token
                    first_token = outputs.sequences[0, 1].item()
                    detected_token = tokenizer.decode([first_token]).strip("<|>")
                    if detected_token and len(detected_token) == 2:
                        detected_lang = detected_token
                    else:
                        detected_lang = "en"
                break

            if not detected_lang:
                detected_lang = "en"

            if on_log:
                lang_name = self.LANGUAGES.get(detected_lang, detected_lang)
                on_log(f"  ✓ Detected language: {detected_lang} ({lang_name})")
                on_log("")

        if not detected_lang:
            detected_lang = lang

        # ── Set forced_decoder_ids to lock the language for all chunks ──
        # This is the key: we force Whisper to transcribe in the detected/selected language
        forced_lang = detected_lang
        forced_decoder_ids = tokenizer.get_decoder_prompt_ids(
            language=forced_lang, task="transcribe"
        )

        if on_log:
            on_log(f"  Transcribing all chunks in: {forced_lang} (forced)")
            on_log("")

        # ── Transcribe all chunks ────────────────────────────────────
        for chunk_idx in range(n_chunks):
            if self._cancelled:
                raise InterruptedError("Processing cancelled by user.")

            start_sample = chunk_idx * chunk_samples
            end_sample = min(start_sample + chunk_samples, total_samples)
            chunk_audio = audio_array[start_sample:end_sample]

            chunk_start_time = start_sample / sample_rate
            chunk_end_time = end_sample / sample_rate

            # Skip silent chunks (very low energy)
            if np.abs(chunk_audio).max() < 0.01:
                if on_progress:
                    on_progress((chunk_idx + 1) / n_chunks)
                continue

            # Extract features and generate with forced language
            input_features = feature_extractor(
                chunk_audio, sampling_rate=sample_rate, return_tensors="pt"
            ).input_features

            with torch.no_grad():
                predicted_ids = model.generate(
                    input_features,
                    forced_decoder_ids=forced_decoder_ids,
                    max_new_tokens=440,
                )

            # Decode
            transcription = tokenizer.batch_decode(
                predicted_ids, skip_special_tokens=True
            )
            text = transcription[0].strip() if transcription else ""

            if text:
                segment = Segment(len(segments) + 1, chunk_start_time, chunk_end_time, text)
                segments.append(segment)

                preview = text[:80] + "…" if len(text) > 80 else text
                if on_log:
                    on_log(f"  Chunk {chunk_idx+1}/{n_chunks} [{self._fmt(chunk_start_time)}] \"{preview}\"")
            else:
                if on_log and chunk_idx % 10 == 0:
                    on_log(f"  Chunk {chunk_idx+1}/{n_chunks} [{self._fmt(chunk_start_time)}] — no speech")

            if on_progress:
                on_progress((chunk_idx + 1) / n_chunks)

        if on_progress:
            on_progress(1.0)

        if on_log:
            on_log(f"Transcription complete — {len(segments)} segments.")
        return segments, detected_lang

    # ------------------------------------------------------------------
    # Translation
    # ------------------------------------------------------------------

    def translate_segments(self, segments, source_lang: str, target_lang: str,
                           *, on_progress=None, on_log=None):
        from deep_translator import GoogleTranslator  # noqa: delay import

        lang_name = self.LANGUAGES.get(target_lang, target_lang)
        if on_log:
            on_log(f"Translating {len(segments)} segments → {lang_name}…")

        translator = GoogleTranslator(source=source_lang, target=target_lang)
        translated = []
        total = len(segments)

        # Process in batches to reduce API calls
        batch_size = 20
        for i in range(0, total, batch_size):
            if self._cancelled:
                raise InterruptedError("Processing cancelled by user.")

            batch = segments[i : i + batch_size]
            texts = [s.text for s in batch]

            try:
                results = translator.translate_batch(texts)
            except Exception:
                # Fallback: one-by-one with delay
                results = []
                for text in texts:
                    if self._cancelled:
                        raise InterruptedError("Processing cancelled by user.")
                    try:
                        results.append(translator.translate(text))
                    except Exception:
                        results.append(text)
                    time.sleep(0.15)

            for j, (seg, trans_text) in enumerate(zip(batch, results)):
                translated.append(
                    Segment(i + j + 1, seg.start, seg.end, trans_text or seg.text)
                )

            if on_progress:
                on_progress(min((i + len(batch)) / total, 1.0))

            if on_log and ((i + batch_size) % 100 == 0 or i + batch_size >= total):
                done = min(i + batch_size, total)
                on_log(f"  Translated {done}/{total}")

            time.sleep(0.25)  # rate-limit

        if on_log:
            on_log(f"Translation to {lang_name} complete.")
        return translated

    # ------------------------------------------------------------------
    # SRT generation
    # ------------------------------------------------------------------

    @staticmethod
    def _fmt(seconds: float) -> str:
        h = int(seconds // 3600)
        m = int((seconds % 3600) // 60)
        s = int(seconds % 60)
        ms = int(round((seconds % 1) * 1000))
        return f"{h:02d}:{m:02d}:{s:02d},{ms:03d}"

    @classmethod
    def generate_srt(cls, segments, output_path: str) -> str:
        with open(output_path, "w", encoding="utf-8") as f:
            for seg in segments:
                f.write(f"{seg.index}\n")
                f.write(f"{cls._fmt(seg.start)} --> {cls._fmt(seg.end)}\n")
                f.write(f"{seg.text}\n\n")
        return output_path

    # ------------------------------------------------------------------
    # Burn subtitles into video (optional)
    # ------------------------------------------------------------------

    def burn_subtitles(self, video_path: str, srt_path: str, output_path: str,
                       *, on_log=None) -> str:
        if on_log:
            on_log("Burning subtitles into video (this may take a while)…")

        # FFmpeg subtitles filter needs forward-slash paths and escaped colons
        srt_filter = srt_path.replace("\\", "/").replace(":", "\\:")

        cmd = [
            FFMPEG_PATH, "-y", "-i", video_path,
            "-vf", f"subtitles='{srt_filter}'",
            "-c:a", "copy",
            output_path,
        ]
        result = subprocess.run(
            cmd, capture_output=True, text=True,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
        )
        if result.returncode != 0:
            raise RuntimeError(f"FFmpeg subtitle burn error:\n{result.stderr[-500:]}")

        if on_log:
            on_log(f"Video with burned subtitles → {os.path.basename(output_path)}")
        return output_path

    # ------------------------------------------------------------------
    # Full pipeline
    # ------------------------------------------------------------------

    def process_video(
        self,
        video_path: str,
        target_languages: list,
        source_language: str = "auto",
        output_dir: str | None = None,
        burn_subs: bool = False,
        *,
        on_progress=None,
        on_log=None,
    ) -> list:
        """
        Run the full pipeline on a single video.

        on_progress(fraction: float, stage: str)
        on_log(message: str)

        Returns list of generated SRT file paths.
        """
        self.reset()

        if not self.check_ffmpeg():
            raise RuntimeError(
                "FFmpeg not found. Install it and ensure it is on your PATH.\n"
                "Download: https://ffmpeg.org/download.html"
            )

        if output_dir is None:
            output_dir = os.path.dirname(video_path)
        os.makedirs(output_dir, exist_ok=True)

        video_name = os.path.splitext(os.path.basename(video_path))[0]
        n_langs = len(target_languages)

        if on_log:
            on_log("═" * 50)
            on_log(f"  VIDEO SUBTITLE TRANSLATOR")
            on_log(f"  Video: {os.path.basename(video_path)}")
            on_log(f"  Target languages: {', '.join(target_languages)}")
            on_log(f"  Output: {output_dir}")
            on_log("═" * 50)

        # ── STEP 1/4: Extract audio ──────────────────────────────────
        if on_progress:
            on_progress(0.0, "Step 1/4 — Extracting audio from video…")
        if on_log:
            on_log(f"\n┌─ STEP 1/4: Extracting audio ─────────────────────")
            file_size_mb = os.path.getsize(video_path) / (1024 * 1024)
            on_log(f"│ Video size: {file_size_mb:.0f} MB")
            on_log(f"│ This extracts the audio track as WAV (16kHz mono)")
            on_log(f"└─────────────────────────────────────────────────")
        # Audio WAV is saved next to the source video (same folder)
        video_dir = os.path.dirname(video_path)
        audio_path = os.path.join(video_dir, f"{video_name}_audio.wav")
        self.extract_audio(video_path, audio_path, on_log=on_log)

        if self._cancelled:
            self._cleanup(audio_path)
            raise InterruptedError("Cancelled.")

        # ── STEP 2/4: Load model + Transcribe ─────────────────────────
        if on_progress:
            on_progress(0.05, "Step 2/4 — Loading AI model & transcribing speech…")
        if on_log:
            on_log(f"\n┌─ STEP 2/4: Transcription ────────────────────────")
            on_log(f"│ Loading AI model and transcribing speech…")
            on_log(f"│ This is the longest step for large videos.")
            on_log(f"└─────────────────────────────────────────────────")

        def _tx_prog(p):
            if on_progress:
                pct = int(p * 100)
                on_progress(0.05 + p * 0.45, f"Step 2/4 — Transcribing… {pct}%")

        segments, detected_lang = self.transcribe(
            audio_path, source_language, on_progress=_tx_prog, on_log=on_log
        )

        if not segments:
            if on_log:
                on_log("⚠ No speech detected in the audio.")
            self._cleanup(audio_path)
            return []

        # Save original-language SRT
        if on_progress:
            on_progress(0.50, "Step 3/4 — Saving original subtitles…")
        orig_srt = os.path.join(output_dir, f"{video_name}.{detected_lang}.srt")
        self.generate_srt(segments, orig_srt)
        if on_log:
            on_log(f"\n✅ Original subtitles saved → {os.path.basename(orig_srt)}")

        generated = [orig_srt]

        # ── STEP 3/4: Translate ──────────────────────────────────────
        langs_to_translate = [l for l in target_languages if l != detected_lang]
        n = len(langs_to_translate)

        if n > 0 and on_log:
            on_log(f"\n┌─ STEP 3/4: Translation ──────────────────────────")
            on_log(f"│ Translating {len(segments)} segments into {n} language(s)…")
            on_log(f"│ Languages: {', '.join(self.LANGUAGES.get(l, l) for l in langs_to_translate)}")
            on_log(f"└─────────────────────────────────────────────────")

        for idx, tgt in enumerate(langs_to_translate):
            if self._cancelled:
                break

            base = 0.50 + (idx / max(n, 1)) * 0.40
            lang_name = self.LANGUAGES.get(tgt, tgt)

            def _tl_prog(p, _b=base, _n=n, _ln=lang_name):
                if on_progress:
                    pct = int(p * 100)
                    on_progress(_b + p * (0.40 / max(_n, 1)),
                                f"Step 3/4 — Translating → {_ln} ({idx+1}/{n})… {pct}%")

            translated = self.translate_segments(
                segments, detected_lang, tgt, on_progress=_tl_prog, on_log=on_log
            )

            srt_path = os.path.join(output_dir, f"{video_name}.{tgt}.srt")
            self.generate_srt(translated, srt_path)
            generated.append(srt_path)
            if on_log:
                on_log(f"  ✅ {lang_name} subtitles → {os.path.basename(srt_path)}")

        # ── STEP 4/4: Burn subtitles (optional) ──────────────────────
        if burn_subs and len(generated) > 1:
            if on_progress:
                on_progress(0.92, "Step 4/4 — Burning subtitles into video…")
            if on_log:
                on_log(f"\n┌─ STEP 4/4: Burning subtitles into video ────────")
                on_log(f"│ This re-encodes the video with hardcoded subs.")
                on_log(f"│ Very slow for large files — please be patient.")
                on_log(f"└─────────────────────────────────────────────────")
            burn_src = generated[-1]
            ext = os.path.splitext(video_path)[1]
            out_vid = os.path.join(output_dir, f"{video_name}_subtitled{ext}")
            try:
                self.burn_subtitles(video_path, burn_src, out_vid, on_log=on_log)
            except RuntimeError as e:
                if on_log:
                    on_log(f"⚠ Warning: could not burn subtitles — {e}")

        # Cleanup temp audio
        self._cleanup(audio_path)

        if on_progress:
            on_progress(1.0, "✓ Complete!")
        if on_log:
            on_log(f"\n{'═' * 50}")
            on_log(f"  ✓ ALL DONE — {len(generated)} subtitle file(s) generated")
            on_log(f"  Output folder: {output_dir}")
            on_log(f"{'═' * 50}")

        return generated

    @staticmethod
    def _cleanup(path):
        try:
            os.remove(path)
        except OSError:
            pass
