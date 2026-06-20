"""
Video Subtitle Translator — GUI Application

Extracts audio from video files, transcribes speech using Whisper,
translates to multiple languages, and generates synchronized SRT subtitles.
"""

import os
import sys
import threading
import time as _time
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
from pathlib import Path

from engine import SubtitleEngine
from player import VLCPlayer, VLC_AVAILABLE

# ──────────────────────────────────────────────────────────────────────
# Default paths
# ──────────────────────────────────────────────────────────────────────
DEFAULT_VIDEO_DIR = str(Path.home() / "Videos" / "Captures")
DEFAULT_OUTPUT_DIR = str(Path.home() / "Documents" / "Subtitles")


def _detect_best_config() -> dict:
    """Auto-detect the best available backend and device at startup."""
    info = {"backend": "ctranslate2", "device": "cpu", "gpu_name": None, "details": ""}

    # Check DirectML (DirectX 12 GPU — works with any modern GPU)
    try:
        import onnxruntime as ort
        providers = ort.get_available_providers()
        if "DmlExecutionProvider" in providers:
            info["backend"] = "directml"
            info["device"] = "DirectML (GPU)"
            # Try to get GPU name
            try:
                import subprocess
                result = subprocess.run(
                    ["wmic", "path", "win32_VideoController", "get", "name"],
                    capture_output=True, text=True,
                    creationflags=subprocess.CREATE_NO_WINDOW,
                )
                lines = [l.strip() for l in result.stdout.splitlines() if l.strip() and l.strip() != "Name"]
                if lines:
                    info["gpu_name"] = lines[0]
            except Exception:
                pass
            info["details"] = f"GPU: {info['gpu_name'] or 'DirectX 12 compatible'} — via DirectML"
            return info
    except Exception:
        pass

    # Check CUDA
    try:
        import ctranslate2
        if ctranslate2.get_cuda_device_count() > 0:
            info["backend"] = "ctranslate2"
            info["device"] = "CUDA (GPU)"
            info["details"] = "NVIDIA GPU — via CUDA + CTranslate2"
            return info
    except Exception:
        pass

    # CPU fallback
    import platform
    cpu = platform.processor() or "Unknown CPU"
    info["details"] = f"CPU: {cpu} — no GPU acceleration available"
    return info


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Video Subtitle Translator")
        self.geometry("1100x800")
        self.minsize(850, 650)
        self.protocol("WM_DELETE_WINDOW", self._on_close)

        self.engine: SubtitleEngine | None = None
        self._worker: threading.Thread | None = None

        # Auto-detect best hardware configuration
        self._hw_config = _detect_best_config()

        self._build_ui()

    # ──────────────────────────────────────────────────────────────────
    # UI construction
    # ──────────────────────────────────────────────────────────────────

    def _build_ui(self):
        pad = dict(padx=8, pady=4)

        # ── Tabbed interface ──────────────────────────────────────────
        self._notebook = ttk.Notebook(self)
        self._notebook.pack(fill="both", expand=True, padx=4, pady=4)

        # Tab 1: Transcription workflow
        self._tab_transcribe = ttk.Frame(self._notebook)
        self._notebook.add(self._tab_transcribe, text="  🎬 Transcribe  ")

        # Tab 2: VLC Player
        self._tab_player = ttk.Frame(self._notebook)
        self._notebook.add(self._tab_player, text="  ▶ Player  ")

        # Build each tab
        self._build_transcribe_tab(pad)
        self._build_player_tab()

    def _build_player_tab(self):
        """Build the embedded VLC player tab."""
        self._vlc_player = VLCPlayer(self._tab_player)
        self._vlc_player.pack(fill="both", expand=True)

    def _build_transcribe_tab(self, pad):
        """Build the transcription workflow tab."""
        parent = self._tab_transcribe

        # ── File selection ────────────────────────────────────────────
        file_frame = ttk.LabelFrame(parent, text="Files", padding=8)
        file_frame.pack(fill="x", **pad)

        ttk.Label(file_frame, text="Input video:").grid(row=0, column=0, sticky="w")
        self.var_input = tk.StringVar()
        ttk.Entry(file_frame, textvariable=self.var_input, width=72).grid(row=0, column=1, sticky="ew", padx=4)
        ttk.Button(file_frame, text="Browse…", command=self._browse_input).grid(row=0, column=2)

        ttk.Label(file_frame, text="Output folder:").grid(row=1, column=0, sticky="w", pady=(4, 0))
        self.var_output = tk.StringVar(value=DEFAULT_OUTPUT_DIR)
        ttk.Entry(file_frame, textvariable=self.var_output, width=72).grid(row=1, column=1, sticky="ew", padx=4, pady=(4, 0))
        ttk.Button(file_frame, text="Browse…", command=self._browse_output).grid(row=1, column=2, pady=(4, 0))

        file_frame.columnconfigure(1, weight=1)

        # ── Model settings ────────────────────────────────────────────
        model_frame = ttk.LabelFrame(parent, text="Whisper settings", padding=8)
        model_frame.pack(fill="x", **pad)

        ttk.Label(model_frame, text="Model size:").grid(row=0, column=0, sticky="w")
        self.var_model = tk.StringVar(value="medium")
        model_combo = ttk.Combobox(
            model_frame, textvariable=self.var_model,
            values=SubtitleEngine.MODEL_SIZES, state="readonly", width=14,
        )
        model_combo.grid(row=0, column=1, sticky="w", padx=4)

        ttk.Label(model_frame, text="Source language:").grid(row=0, column=2, sticky="w", padx=(16, 0))
        self.var_source_lang = tk.StringVar(value="auto")
        src_values = [f"{code} — {name}" for code, name in SubtitleEngine.LANGUAGES.items()]
        src_combo = ttk.Combobox(
            model_frame, textvariable=self.var_source_lang,
            values=src_values, state="readonly", width=22,
        )
        src_combo.current(0)
        src_combo.grid(row=0, column=3, sticky="w", padx=4)

        # ── Hardware status (auto-detected, read-only) ────────────────
        hw_frame = ttk.LabelFrame(parent, text="Hardware (auto-detected)", padding=8)
        hw_frame.pack(fill="x", **pad)

        # Backend & device indicator
        backend_text = self._hw_config["backend"].upper()
        device_text = self._hw_config["device"]
        detail_text = self._hw_config["details"]

        # Status icon
        if "GPU" in device_text:
            icon = "🟢"
            status_msg = f"{icon}  {backend_text} — {device_text}"
        else:
            icon = "🟡"
            status_msg = f"{icon}  {backend_text} — {device_text} (slower)"

        status_label = ttk.Label(hw_frame, text=status_msg, font=("Segoe UI", 10, "bold"))
        status_label.pack(anchor="w")

        detail_label = ttk.Label(hw_frame, text=detail_text, foreground="gray")
        detail_label.pack(anchor="w", pady=(2, 0))

        # ── Target languages ──────────────────────────────────────────
        lang_frame = ttk.LabelFrame(parent, text="Target languages (select one or more)", padding=8)
        lang_frame.pack(fill="x", **pad)

        self.lang_vars: dict[str, tk.BooleanVar] = {}
        lang_items = [(c, n) for c, n in SubtitleEngine.LANGUAGES.items() if c != "auto"]
        cols = 6
        for i, (code, name) in enumerate(lang_items):
            var = tk.BooleanVar(value=(code in ("it", "en")))
            self.lang_vars[code] = var
            cb = ttk.Checkbutton(lang_frame, text=f"{name} ({code})", variable=var)
            cb.grid(row=i // cols, column=i % cols, sticky="w", padx=4, pady=1)

        # Quick-select buttons
        btn_row = ttk.Frame(lang_frame)
        btn_row.grid(row=(len(lang_items) // cols) + 1, column=0, columnspan=cols, sticky="w", pady=(4, 0))
        ttk.Button(btn_row, text="Select all", command=self._select_all_langs).pack(side="left", padx=2)
        ttk.Button(btn_row, text="Clear all", command=self._clear_all_langs).pack(side="left", padx=2)

        # ── Options ───────────────────────────────────────────────────
        opt_frame = ttk.Frame(parent)
        opt_frame.pack(fill="x", **pad)

        self.var_burn = tk.BooleanVar(value=False)
        ttk.Checkbutton(opt_frame, text="Burn subtitles into video copy (slow for large files)",
                         variable=self.var_burn).pack(side="left")

        # ── Action buttons ────────────────────────────────────────────
        btn_frame = ttk.Frame(parent)
        btn_frame.pack(fill="x", **pad)

        self.btn_start = ttk.Button(btn_frame, text="▶  Start Processing", command=self._start)
        self.btn_start.pack(side="left", padx=4)

        self.btn_cancel = ttk.Button(btn_frame, text="■  Cancel", command=self._cancel, state="disabled")
        self.btn_cancel.pack(side="left", padx=4)

        self.btn_open_folder = ttk.Button(btn_frame, text="📂 Open output folder", command=self._open_output)
        self.btn_open_folder.pack(side="left", padx=4)

        self.btn_play_result = ttk.Button(btn_frame, text="▶ Play in Player", command=self._play_result, state="disabled")
        self.btn_play_result.pack(side="left", padx=4)

        # ── Progress ──────────────────────────────────────────────────
        prog_frame = ttk.LabelFrame(parent, text="Progress", padding=8)
        prog_frame.pack(fill="x", **pad)

        # Current step indicator (large, bold)
        self.var_stage = tk.StringVar(value="⏸ Ready — select a video and click Start")
        stage_label = ttk.Label(prog_frame, textvariable=self.var_stage,
                                 font=("Segoe UI", 11, "bold"))
        stage_label.pack(anchor="w")

        # Sub-status with detail
        self.var_detail = tk.StringVar(value="")
        detail_label = ttk.Label(prog_frame, textvariable=self.var_detail,
                                  foreground="gray")
        detail_label.pack(anchor="w", pady=(2, 4))

        # Progress bar + percentage + elapsed time
        bar_frame = ttk.Frame(prog_frame)
        bar_frame.pack(fill="x")

        self.progress = ttk.Progressbar(bar_frame, length=300, mode="determinate")
        self.progress.pack(side="left", fill="x", expand=True)

        self.var_pct = tk.StringVar(value="0%")
        ttk.Label(bar_frame, textvariable=self.var_pct, width=6).pack(side="left", padx=(8, 0))

        self.var_elapsed = tk.StringVar(value="⏱ 00:00")
        ttk.Label(bar_frame, textvariable=self.var_elapsed, width=12).pack(side="left", padx=(8, 0))

        # Timer state
        self._start_time: float | None = None
        self._timer_id: str | None = None

        # ── Log ───────────────────────────────────────────────────────
        log_frame = ttk.LabelFrame(parent, text="Log", padding=4)
        log_frame.pack(fill="both", expand=True, **pad)

        self.log_text = tk.Text(log_frame, wrap="word", height=14, state="disabled",
                                 font=("Consolas", 9), bg="#1e1e1e", fg="#d4d4d4",
                                 insertbackground="#d4d4d4")
        scrollbar = ttk.Scrollbar(log_frame, orient="vertical", command=self.log_text.yview)
        self.log_text.configure(yscrollcommand=scrollbar.set)
        scrollbar.pack(side="right", fill="y")
        self.log_text.pack(fill="both", expand=True)

    # ──────────────────────────────────────────────────────────────────
    # Callbacks
    # ──────────────────────────────────────────────────────────────────

    def _browse_input(self):
        start = DEFAULT_VIDEO_DIR if os.path.isdir(DEFAULT_VIDEO_DIR) else str(Path.home())
        path = filedialog.askopenfilename(
            initialdir=start,
            title="Select a video file",
            filetypes=[
                ("Video files", "*.mp4 *.mkv *.avi *.mov *.webm *.wmv *.flv"),
                ("All files", "*.*"),
            ],
        )
        if path:
            self.var_input.set(path)

    def _browse_output(self):
        path = filedialog.askdirectory(
            initialdir=self.var_output.get() or str(Path.home()),
            title="Select output folder",
        )
        if path:
            self.var_output.set(path)

    def _select_all_langs(self):
        for var in self.lang_vars.values():
            var.set(True)

    def _clear_all_langs(self):
        for var in self.lang_vars.values():
            var.set(False)

    def _open_output(self):
        folder = self.var_output.get()
        if os.path.isdir(folder):
            os.startfile(folder)

    def _play_result(self):
        """Open the processed video in the embedded player with subtitles."""
        video = self.var_input.get().strip()
        if not video or not os.path.isfile(video):
            return

        # Switch to Player tab
        self._notebook.select(self._tab_player)

        # Load video (VLC subtitle rendering disabled)
        self._vlc_player.load_video(video)

        # Load all generated SRT files into custom subtitle system
        if hasattr(self, "_last_generated") and self._last_generated:
            srt_files = [f for f in self._last_generated if f.endswith(".srt")]
            if srt_files:
                self._vlc_player.load_all_subtitles(srt_files)

    def _log(self, message: str):
        """Thread-safe log append."""
        def _append():
            self.log_text.configure(state="normal")
            self.log_text.insert("end", message + "\n")
            self.log_text.see("end")
            self.log_text.configure(state="disabled")
        self.after(0, _append)

    def _set_progress(self, fraction: float, stage: str = ""):
        def _update():
            self.progress["value"] = fraction * 100
            self.var_pct.set(f"{fraction:.0%}")
            if stage:
                self.var_stage.set(f"▶ {stage}")
        self.after(0, _update)

    def _set_detail(self, detail: str):
        def _update():
            self.var_detail.set(detail)
        self.after(0, _update)

    def _start_timer(self):
        self._start_time = _time.time()
        self._tick_timer()

    def _tick_timer(self):
        if self._start_time is None:
            return
        elapsed = _time.time() - self._start_time
        m, s = divmod(int(elapsed), 60)
        h, m = divmod(m, 60)
        if h > 0:
            self.var_elapsed.set(f"⏱ {h}:{m:02d}:{s:02d}")
        else:
            self.var_elapsed.set(f"⏱ {m:02d}:{s:02d}")
        self._timer_id = self.after(1000, self._tick_timer)

    def _stop_timer(self):
        if self._timer_id:
            self.after_cancel(self._timer_id)
            self._timer_id = None
        self._start_time = None

    # ──────────────────────────────────────────────────────────────────
    # Processing
    # ──────────────────────────────────────────────────────────────────

    def _validate(self) -> bool:
        video = self.var_input.get().strip()
        if not video or not os.path.isfile(video):
            messagebox.showerror("Error", "Please select a valid video file.")
            return False

        targets = [code for code, var in self.lang_vars.items() if var.get()]
        if not targets:
            messagebox.showerror("Error", "Please select at least one target language.")
            return False

        return True

    def _start(self):
        if not self._validate():
            return

        self.btn_start.configure(state="disabled")
        self.btn_cancel.configure(state="normal")

        # Clear log
        self.log_text.configure(state="normal")
        self.log_text.delete("1.0", "end")
        self.log_text.configure(state="disabled")

        # Reset progress display
        self.var_pct.set("0%")
        self.var_elapsed.set("⏱ 00:00")
        self.var_detail.set("Initializing…")
        self._start_timer()

        self._worker = threading.Thread(target=self._run_pipeline, daemon=True)
        self._worker.start()

    def _cancel(self):
        if self.engine:
            self.engine.cancel()
            self._log("Cancellation requested…")

    def _run_pipeline(self):
        video = self.var_input.get().strip()
        output_dir = self.var_output.get().strip() or DEFAULT_OUTPUT_DIR
        targets = [code for code, var in self.lang_vars.items() if var.get()]

        # Parse source language from combo value like "en — English"
        src_raw = self.var_source_lang.get()
        source_lang = src_raw.split(" — ")[0] if " — " in src_raw else src_raw

        model_size = self.var_model.get()
        # Use auto-detected best configuration
        backend = self._hw_config["backend"]
        device = self._hw_config["device"]
        burn = self.var_burn.get()

        self._log(f"Hardware: {self._hw_config['details']}")
        self._log(f"Backend: {backend} | Device: {device}")
        self._log("")

        self.engine = SubtitleEngine(model_size=model_size, device="auto", backend=backend)

        try:
            generated = self.engine.process_video(
                video_path=video,
                target_languages=targets,
                source_language=source_lang,
                output_dir=output_dir,
                burn_subs=burn,
                on_progress=self._set_progress,
                on_log=self._log,
            )
            if generated:
                self._last_generated = generated
                self._log(f"\nGenerated files:")
                for f in generated:
                    self._log(f"  • {f}")
                self.after(0, lambda: self.btn_play_result.configure(state="normal"))
                self.after(0, lambda: messagebox.showinfo(
                    "Done",
                    f"Generated {len(generated)} subtitle file(s).\n\n"
                    f"Output: {output_dir}\n\n"
                    "Click '▶ Play in Player' to watch the video with subtitles."
                ))
        except InterruptedError:
            self._log("\n⚠ Processing cancelled.")
            self._set_progress(0, "⏹ Cancelled")
        except Exception as e:
            self._log(f"\n❌ ERROR: {e}")
            self.after(0, lambda: messagebox.showerror("Error", str(e)))
            self._set_progress(0, "❌ Error — see log")
        finally:
            self.after(0, self._reset_ui)

    def _reset_ui(self):
        self.btn_start.configure(state="normal")
        self.btn_cancel.configure(state="disabled")
        self._stop_timer()
        self.var_detail.set("")

    def _on_close(self):
        if self._worker and self._worker.is_alive():
            if not messagebox.askyesno("Confirm", "Processing is running. Cancel and exit?"):
                return
            if self.engine:
                self.engine.cancel()
        if hasattr(self, "_vlc_player"):
            self._vlc_player.release()
        self.destroy()


def main():
    app = App()
    app.mainloop()


if __name__ == "__main__":
    main()
