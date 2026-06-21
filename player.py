"""
Embedded VLC Player — plays video with custom subtitle rendering.

Subtitles are rendered via tkinter (not VLC's built-in renderer) so we
get full control over font, size, colour, position and language switching.

Requires VLC media player installed on the system (uses libVLC).
"""

import os
import re
import sys
import tkinter as tk
from tkinter import ttk, filedialog, colorchooser
from pathlib import Path


def _find_vlc() -> str | None:
    """Try to locate VLC installation directory on Windows."""
    candidates = [
        os.environ.get("PROGRAMFILES", r"C:\Program Files") + r"\VideoLAN\VLC",
        os.environ.get("PROGRAMFILES(X86)", r"C:\Program Files (x86)") + r"\VideoLAN\VLC",
        str(Path.home() / "AppData" / "Local" / "Programs" / "VideoLAN" / "VLC"),
    ]
    for path in candidates:
        if os.path.isfile(os.path.join(path, "libvlc.dll")):
            return path
    return None


# Add VLC to DLL search path BEFORE importing vlc module
if sys.platform == "win32":
    _vlc_dir = _find_vlc()
    if _vlc_dir:
        os.environ["PATH"] = _vlc_dir + ";" + os.environ.get("PATH", "")
        if hasattr(os, "add_dll_directory"):
            os.add_dll_directory(_vlc_dir)

try:
    import vlc
    VLC_AVAILABLE = True
except OSError:
    VLC_AVAILABLE = False


# ──────────────────────────────────────────────────────────────────────
# SRT parser
# ──────────────────────────────────────────────────────────────────────

_TS_RE = re.compile(
    r"(\d{2}):(\d{2}):(\d{2})[,.](\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2})[,.](\d{3})"
)


def _parse_ts(h, m, s, ms) -> int:
    """Convert h:m:s,ms to milliseconds."""
    return int(h) * 3600000 + int(m) * 60000 + int(s) * 1000 + int(ms)


def parse_srt(path: str) -> list[dict]:
    """Parse an SRT file into a list of {start_ms, end_ms, text}."""
    entries: list[dict] = []
    if not os.path.isfile(path):
        return entries
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        content = f.read()

    blocks = re.split(r"\n\s*\n", content.strip())
    for block in blocks:
        lines = block.strip().splitlines()
        if len(lines) < 2:
            continue
        # Find the timestamp line
        for i, line in enumerate(lines):
            m = _TS_RE.match(line.strip())
            if m:
                start = _parse_ts(m.group(1), m.group(2), m.group(3), m.group(4))
                end = _parse_ts(m.group(5), m.group(6), m.group(7), m.group(8))
                text = "\n".join(lines[i + 1:]).strip()
                # Strip basic HTML tags
                text = re.sub(r"<[^>]+>", "", text)
                if text:
                    entries.append({"start": start, "end": end, "text": text})
                break
    return entries


# ──────────────────────────────────────────────────────────────────────
# Subtitle display modes
# ──────────────────────────────────────────────────────────────────────
MODE_OVERLAY = "overlay"
MODE_PANEL = "panel"


class VLCPlayer(ttk.Frame):
    """Embedded VLC video player with custom subtitle rendering."""

    def __init__(self, parent, **kwargs):
        super().__init__(parent, **kwargs)

        self._instance: vlc.Instance | None = None
        self._player: vlc.MediaPlayer | None = None
        self._current_video: str = ""
        self._is_playing = False
        self._update_id: str | None = None

        # Subtitle state
        self._srt_files: dict[str, list[dict]] = {}   # label → parsed entries
        self._srt_paths: dict[str, str] = {}           # label → file path
        self._current_sub_label: str = ""
        self._last_sub_text: str = ""

        # Subtitle style defaults
        self._sub_font_family = "Segoe UI"
        self._sub_font_size = 16
        self._sub_fg = "#FFFFFF"
        self._sub_bg = "#000000"
        self._sub_bg_opacity = 0.7  # 0-1
        self._sub_mode = MODE_OVERLAY

        self._init_vlc()
        self._build_ui()

    def _init_vlc(self):
        if not VLC_AVAILABLE:
            return
        try:
            # Disable VLC's own subtitle rendering
            self._instance = vlc.Instance("--no-xlib", "--quiet", "--no-sub-autodetect-file")
            self._player = self._instance.media_player_new()
        except Exception:
            self._instance = None
            self._player = None

    # ──────────────────────────────────────────────────────────────────
    # UI
    # ──────────────────────────────────────────────────────────────────

    def _build_ui(self):
        if not self._player:
            err = ttk.Frame(self)
            err.pack(fill="both", expand=True)
            ttk.Label(
                err,
                text="⚠ VLC non trovato!\n\n"
                     "Per usare il player integrato, installa VLC:\n"
                     "https://www.videolan.org/vlc/\n\n"
                     "Dopo l'installazione, riavvia l'applicazione.",
                font=("Segoe UI", 12), justify="center",
            ).pack(expand=True)
            return

        # Main PanedWindow: video area (top) + subtitle panel (bottom)
        self._paned = ttk.PanedWindow(self, orient="vertical")
        self._paned.pack(fill="both", expand=True)

        # ── Video container (with overlay subtitle) ───────────────────
        self._video_container = tk.Frame(self._paned, bg="black")
        self._paned.add(self._video_container, weight=3)

        self._video_frame = tk.Frame(self._video_container, bg="black")
        self._video_frame.pack(fill="both", expand=True)

        # Overlay subtitle label (on top of video)
        self._overlay_pos_y = 0.92
        self._overlay_label = tk.Label(
            self._video_container,
            text="", font=(self._sub_font_family, self._sub_font_size),
            fg=self._sub_fg, bg=self._sub_bg,
            wraplength=800, justify="center", padx=10, pady=4,
        )
        self._overlay_label.place_forget()  # hidden until needed

        # ── Subtitle text panel (alternative to overlay) ──────────────
        self._sub_panel = tk.Frame(self._paned, bg="#1a1a2e")

        self._sub_panel_text = tk.Label(
            self._sub_panel,
            text="", font=(self._sub_font_family, self._sub_font_size),
            fg=self._sub_fg, bg="#1a1a2e",
            wraplength=900, justify="center", padx=16, pady=12,
            anchor="center",
        )
        self._sub_panel_text.pack(fill="both", expand=True)

        # ── Controls ──────────────────────────────────────────────────
        # Controls can be embedded or detached into a separate window
        self._controls_detached = False
        self._controls_window = None

        self._controls_frame = ttk.Frame(self)
        self._controls_frame.pack(fill="x", padx=4, pady=4)
        self._build_controls(self._controls_frame)

    def _build_controls(self, parent):
        """Build all control widgets inside the given parent frame."""
        # Only create StringVars once (survive detach/attach)
        if not hasattr(self, "var_video_name") or self.var_video_name is None:
            self.var_video_name = tk.StringVar(value="No video loaded")
        if not hasattr(self, "_detach_btn_text") or self._detach_btn_text is None:
            self._detach_btn_text = tk.StringVar(
                value="⇙ Attach" if self._controls_detached else "⇗ Detach"
            )
        if not hasattr(self, "var_time") or self.var_time is None:
            self.var_time = tk.StringVar(value="00:00:00 / 00:00:00")
        if not hasattr(self, "_seek_var") or self._seek_var is None:
            self._seek_var = tk.DoubleVar(value=0)
        if not hasattr(self, "_vol_var") or self._vol_var is None:
            self._vol_var = tk.IntVar(value=80)
        if not hasattr(self, "var_sub_lang") or self.var_sub_lang is None:
            self.var_sub_lang = tk.StringVar(value="— none —")
        if not hasattr(self, "_var_mode_combo") or self._var_mode_combo is None:
            self._var_mode_combo = tk.StringVar(value="Overlay")

        # Row 1: file buttons + detach toggle
        file_row = ttk.Frame(parent)
        file_row.pack(fill="x", pady=(0, 3))

        ttk.Button(file_row, text="📂 Open Video", command=self._open_video).pack(side="left", padx=2)
        ttk.Button(file_row, text="📝 Add Subtitle", command=self._add_subtitle).pack(side="left", padx=2)
        ttk.Label(file_row, textvariable=self.var_video_name, foreground="gray").pack(side="left", padx=8)

        ttk.Button(file_row, textvariable=self._detach_btn_text,
                   command=self._toggle_detach).pack(side="right", padx=2)

        # Row 2: playback
        play_row = ttk.Frame(parent)
        play_row.pack(fill="x", pady=(0, 3))

        self.btn_play = ttk.Button(play_row, text="▶", width=4, command=self._toggle_play)
        self.btn_play.pack(side="left", padx=2)
        ttk.Button(play_row, text="■", width=4, command=self._stop).pack(side="left", padx=2)
        ttk.Button(play_row, text="⏪ -10s", width=7,
                   command=lambda: self._seek_relative(-10000)).pack(side="left", padx=2)
        ttk.Button(play_row, text="⏩ +10s", width=7,
                   command=lambda: self._seek_relative(10000)).pack(side="left", padx=2)

        ttk.Label(play_row, textvariable=self.var_time, font=("Consolas", 9)).pack(side="right", padx=8)

        # Row 3: seek bar
        seek_row = ttk.Frame(parent)
        seek_row.pack(fill="x", pady=(0, 3))

        self._seek_scale = ttk.Scale(
            seek_row, from_=0, to=1000, variable=self._seek_var,
            orient="horizontal", command=self._on_seek,
        )
        self._seek_scale.pack(fill="x", expand=True)

        # Row 4: volume
        vol_row = ttk.Frame(parent)
        vol_row.pack(fill="x", pady=(0, 3))

        ttk.Label(vol_row, text="🔊").pack(side="left")
        ttk.Scale(
            vol_row, from_=0, to=100, variable=self._vol_var,
            orient="horizontal", length=120, command=self._on_volume,
        ).pack(side="left", padx=4)

        # ── Row 5: Subtitle controls (clearly labelled section) ──────
        sub_frame = ttk.LabelFrame(parent, text="Subtitles", padding=6)
        sub_frame.pack(fill="x", pady=(4, 0))

        sub_top = ttk.Frame(sub_frame)
        sub_top.pack(fill="x")

        ttk.Label(sub_top, text="Language:").pack(side="left", padx=(0, 4))
        self._lang_combo = ttk.Combobox(
            sub_top, textvariable=self.var_sub_lang,
            values=["— none —"] + list(self._srt_files.keys()),
            state="readonly", width=28,
        )
        self._lang_combo.pack(side="left", padx=2)
        self._lang_combo.bind("<<ComboboxSelected>>", self._on_lang_changed)

        ttk.Label(sub_top, text="  Display:").pack(side="left", padx=(12, 4))
        mode_combo = ttk.Combobox(
            sub_top, textvariable=self._var_mode_combo,
            values=["Overlay", "Panel"], state="readonly", width=10,
        )
        mode_combo.pack(side="left", padx=2)
        mode_combo.bind("<<ComboboxSelected>>", self._on_mode_changed)

        ttk.Button(sub_top, text="🎨 Style…", command=self._open_style_dialog).pack(side="right", padx=2)

    # ──────────────────────────────────────────────────────────────────
    # Display mode switching
    # ──────────────────────────────────────────────────────────────────

    def _on_mode_changed(self, event=None):
        sel = self._var_mode_combo.get()
        new_mode = MODE_PANEL if sel == "Panel" else MODE_OVERLAY
        if new_mode != self._sub_mode:
            self._sub_mode = new_mode
            self._apply_display_mode()
            self._last_sub_text = ""  # force re-render

    def _apply_display_mode(self):
        if self._sub_mode == MODE_PANEL:
            self._overlay_label.place_forget()
            panes = self._paned.panes()
            if len(panes) < 2:
                self._paned.add(self._sub_panel, weight=1)
        else:
            try:
                self._paned.forget(self._sub_panel)
            except Exception:
                pass

    # ──────────────────────────────────────────────────────────────────
    # Detach / Attach controls
    # ──────────────────────────────────────────────────────────────────

    def _toggle_detach(self):
        if self._controls_detached:
            self._attach_controls()
        else:
            self._detach_controls()

    def _detach_controls(self):
        """Move controls into a separate floating window."""
        if self._controls_detached:
            return

        # Destroy current embedded controls
        self._controls_frame.pack_forget()
        for widget in self._controls_frame.winfo_children():
            widget.destroy()

        # Create floating window
        self._controls_window = tk.Toplevel(self)
        self._controls_window.title("Player Controls")
        self._controls_window.transient(self.winfo_toplevel())
        self._controls_window.protocol("WM_DELETE_WINDOW", self._attach_controls)
        self._controls_window.geometry("580x220")
        self._controls_window.minsize(400, 180)

        # Rebuild controls inside floating window
        inner = ttk.Frame(self._controls_window, padding=6)
        inner.pack(fill="both", expand=True)
        self._build_controls(inner)

        self._controls_detached = True
        self._detach_btn_text.set("⇙ Attach")

    def _attach_controls(self):
        """Bring controls back into the main widget."""
        if not self._controls_detached:
            return

        # Destroy floating window
        if self._controls_window:
            for widget in self._controls_window.winfo_children():
                widget.destroy()
            self._controls_window.destroy()
            self._controls_window = None

        # Rebuild controls inside embedded frame
        self._controls_frame.pack(fill="x", padx=4, pady=4)
        self._build_controls(self._controls_frame)

        self._controls_detached = False
        self._detach_btn_text.set("⇗ Detach")

    # ──────────────────────────────────────────────────────────────────
    # Style settings dialog
    # ──────────────────────────────────────────────────────────────────

    def _open_style_dialog(self):
        # Singleton: if already open, just bring to front
        if hasattr(self, "_style_dlg") and self._style_dlg:
            try:
                if self._style_dlg.winfo_exists():
                    self._style_dlg.lift()
                    self._style_dlg.focus_force()
                    return
            except Exception:
                pass
            self._style_dlg = None

        dlg = tk.Toplevel(self)
        self._style_dlg = dlg
        dlg.title("Subtitle Style Settings")
        dlg.transient(self.winfo_toplevel())
        dlg.protocol("WM_DELETE_WINDOW", self._close_style_dlg)

        pad = dict(padx=10, pady=5)

        # Helper: apply settings live to subtitle widgets
        def apply_live(*_args):
            try:
                family = var_font.get()
                size = var_size.get()
            except Exception:
                return
            self._sub_font_family = family
            self._sub_font_size = size
            self._overlay_label.configure(
                font=(family, size), fg=self._sub_fg, bg=self._sub_bg,
            )
            self._sub_panel_text.configure(
                font=(family, size), fg=self._sub_fg,
            )
            preview.configure(font=(family, size), fg=self._sub_fg, bg=self._sub_bg)
            self._last_sub_text = ""

        # ── Font ──────────────────────────────────────────────────────
        font_frame = ttk.LabelFrame(dlg, text="Font", padding=8)
        font_frame.pack(fill="x", **pad)

        ttk.Label(font_frame, text="Family:").grid(row=0, column=0, sticky="w")
        var_font = tk.StringVar(value=self._sub_font_family)
        font_combo = ttk.Combobox(
            font_frame, textvariable=var_font, width=18,
            values=["Segoe UI", "Arial", "Helvetica", "Consolas",
                    "Courier New", "Georgia", "Verdana", "Tahoma"],
        )
        font_combo.grid(row=0, column=1, padx=4)
        font_combo.bind("<<ComboboxSelected>>", apply_live)
        var_font.trace_add("write", apply_live)

        ttk.Label(font_frame, text="Size:").grid(row=0, column=2, sticky="w", padx=(12, 0))
        var_size = tk.IntVar(value=self._sub_font_size)
        ttk.Spinbox(font_frame, from_=8, to=48, textvariable=var_size,
                     width=5, command=apply_live).grid(row=0, column=3, padx=4)
        var_size.trace_add("write", apply_live)

        # ── Colours ──────────────────────────────────────────────────
        color_frame = ttk.LabelFrame(dlg, text="Colours", padding=8)
        color_frame.pack(fill="x", **pad)

        ttk.Label(color_frame, text="Text:").grid(row=0, column=0, sticky="w")
        fg_swatch = tk.Label(color_frame, bg=self._sub_fg, width=4, relief="sunken")
        fg_swatch.grid(row=0, column=1, padx=4)

        def pick_fg():
            color = colorchooser.askcolor(color=self._sub_fg, title="Text colour", parent=dlg)
            if color[1]:
                self._sub_fg = color[1]
                fg_swatch.configure(bg=color[1])
                apply_live()

        ttk.Button(color_frame, text="Pick…", command=pick_fg).grid(row=0, column=2, padx=4)

        ttk.Label(color_frame, text="Background:").grid(row=1, column=0, sticky="w", pady=(4, 0))
        bg_swatch = tk.Label(color_frame, bg=self._sub_bg, width=4, relief="sunken")
        bg_swatch.grid(row=1, column=1, padx=4, pady=(4, 0))

        def pick_bg():
            color = colorchooser.askcolor(color=self._sub_bg, title="Background colour", parent=dlg)
            if color[1]:
                self._sub_bg = color[1]
                bg_swatch.configure(bg=color[1])
                apply_live()

        ttk.Button(color_frame, text="Pick…", command=pick_bg).grid(row=1, column=2, padx=4, pady=(4, 0))

        # ── Position (overlay) ───────────────────────────────────────
        pos_frame = ttk.LabelFrame(dlg, text="Position (overlay mode)", padding=8)
        pos_frame.pack(fill="x", **pad)

        ttk.Label(pos_frame, text="Vertical:").pack(side="left")
        var_pos_y = tk.DoubleVar(value=self._overlay_pos_y)

        def on_pos(*_):
            self._overlay_pos_y = var_pos_y.get()

        ttk.Scale(pos_frame, from_=0.1, to=1.0, variable=var_pos_y,
                  orient="horizontal", length=200, command=on_pos).pack(side="left", padx=8)
        ttk.Label(pos_frame, text="↑ top    ↓ bottom").pack(side="left", foreground="gray")

        # ── Preview ──────────────────────────────────────────────────
        prev_frame = ttk.LabelFrame(dlg, text="Preview", padding=4)
        prev_frame.pack(fill="x", **pad)

        preview = tk.Label(
            prev_frame, text="Subtitle preview — Anteprima sottotitoli",
            font=(self._sub_font_family, self._sub_font_size),
            fg=self._sub_fg, bg=self._sub_bg, padx=10, pady=6,
        )
        preview.pack(fill="x")

        # ── Close ────────────────────────────────────────────────────
        ttk.Button(dlg, text="Close", command=self._close_style_dlg).pack(pady=8)

    def _close_style_dlg(self):
        if hasattr(self, "_style_dlg") and self._style_dlg:
            try:
                self._style_dlg.destroy()
            except Exception:
                pass
        self._style_dlg = None

    # ──────────────────────────────────────────────────────────────────
    # Public API
    # ──────────────────────────────────────────────────────────────────

    def load_video(self, video_path: str, subtitle_path: str | None = None):
        """Load a video file. Does NOT auto-load subtitles via VLC."""
        if not self._player or not os.path.isfile(video_path):
            return

        self._stop()
        self._current_video = video_path
        media = self._instance.media_new(video_path)
        # Disable VLC subtitle rendering completely
        media.add_option(":no-sub-autodetect-file")
        media.add_option(":sub-track=-1")
        self._player.set_media(media)

        if sys.platform == "win32":
            self._player.set_hwnd(self._video_frame.winfo_id())

        self.var_video_name.set(os.path.basename(video_path))
        self._player.audio_set_volume(self._vol_var.get())

        # Disable VLC SPU (subtitle processing unit)
        self.after(300, lambda: self._player.video_set_spu(-1) if self._player else None)

        # Auto-discover SRT files matching the video name
        self._auto_discover_srt(video_path)

        self._play()

    def _auto_discover_srt(self, video_path: str):
        """Find .srt files in the same folder whose name starts with the video stem.

        E.g. for 'MyVideo.mp4', loads 'MyVideo_en.srt', 'MyVideo_it.srt', etc.
        """
        folder = os.path.dirname(video_path)
        if not os.path.isdir(folder):
            return
        video_stem = Path(video_path).stem
        srt_files = sorted(Path(folder).glob("*.srt"))
        for srt in srt_files:
            # Only load SRTs whose name starts with the video name
            if not srt.stem.startswith(video_stem):
                continue
            srt_str = str(srt)
            # Skip already loaded
            if srt_str in self._srt_paths.values():
                continue
            entries = parse_srt(srt_str)
            if not entries:
                continue
            # Extract language label: "MyVideo_en" → "en"
            suffix = srt.stem[len(video_stem):]
            if suffix.startswith("_") and len(suffix) > 1:
                label = suffix[1:]  # strip leading underscore
            else:
                label = srt.stem
            if label in self._srt_files:
                label = srt.name
            self._srt_files[label] = entries
            self._srt_paths[label] = srt_str

        # Update combo
        values = ["— none —"] + list(self._srt_files.keys())
        self._lang_combo.configure(values=values)

        # Auto-select the first subtitle if none selected yet
        if not self._current_sub_label and self._srt_files:
            first = list(self._srt_files.keys())[0]
            self.var_sub_lang.set(first)
            self._current_sub_label = first

    def load_subtitle(self, srt_path: str):
        """Load an SRT file and auto-discover sibling SRT files with the same base name."""
        if not os.path.isfile(srt_path):
            return

        # If we have a current video loaded, discover SRTs matching that video name
        if self._current_video:
            self._auto_discover_srt(self._current_video)

        # Make sure this specific file is loaded even if it didn't match video stem
        srt_str = os.path.normpath(srt_path)
        if srt_str not in [os.path.normpath(p) for p in self._srt_paths.values()]:
            entries = parse_srt(srt_path)
            if entries:
                stem = Path(srt_path).stem
                parts = stem.rsplit("_", 1)
                label = parts[-1] if len(parts) > 1 else stem
                if label in self._srt_files:
                    label = Path(srt_path).name
                self._srt_files[label] = entries
                self._srt_paths[label] = srt_path
                values = ["— none —"] + list(self._srt_files.keys())
                self._lang_combo.configure(values=values)

        # Find which label corresponds to this path and select it
        label = Path(srt_path).stem
        for lbl, path in self._srt_paths.items():
            if os.path.normpath(path) == os.path.normpath(srt_path):
                label = lbl
                break

        # Select the explicitly added one
        self.var_sub_lang.set(label)
        self._current_sub_label = label

    def load_all_subtitles(self, srt_paths: list[str]):
        """Load multiple SRT files at once without auto-selecting."""
        for path in srt_paths:
            if not os.path.isfile(path):
                continue
            entries = parse_srt(path)
            if not entries:
                continue
            stem = Path(path).stem
            parts = stem.rsplit("_", 1)
            label = parts[-1] if len(parts) > 1 else stem
            if label in self._srt_files:
                label = Path(path).name
            self._srt_files[label] = entries
            self._srt_paths[label] = path

        values = ["— none —"] + list(self._srt_files.keys())
        self._lang_combo.configure(values=values)

        # Don't auto-select any — let user choose
        if not self._current_sub_label and self._srt_files:
            # Select the first language by default
            first = list(self._srt_files.keys())[0]
            self.var_sub_lang.set(first)
            self._current_sub_label = first

    # ──────────────────────────────────────────────────────────────────
    # Internal controls
    # ──────────────────────────────────────────────────────────────────

    def _open_video(self):
        path = filedialog.askopenfilename(
            title="Select a video file",
            filetypes=[
                ("Video files", "*.mp4 *.mkv *.avi *.mov *.webm *.wmv *.flv *.ts"),
                ("All files", "*.*"),
            ],
        )
        if path:
            self.load_video(path)

    def _add_subtitle(self):
        paths = filedialog.askopenfilenames(
            title="Select subtitle file(s)",
            filetypes=[
                ("Subtitle files", "*.srt *.ass *.ssa *.sub *.vtt"),
                ("All files", "*.*"),
            ],
        )
        for path in paths:
            self.load_subtitle(path)

    def _on_lang_changed(self, event=None):
        sel = self.var_sub_lang.get()
        if sel == "— none —":
            self._current_sub_label = ""
            self._hide_subtitle()
        else:
            self._current_sub_label = sel
            self._last_sub_text = ""  # force re-render

    def _toggle_play(self):
        if not self._player:
            return
        if self._player.is_playing():
            self._pause()
        else:
            self._play()

    def _play(self):
        if not self._player:
            return
        self._player.play()
        self._is_playing = True
        self.btn_play.configure(text="⏸")
        self._start_update_loop()

    def _pause(self):
        if not self._player:
            return
        self._player.pause()
        self._is_playing = False
        self.btn_play.configure(text="▶")

    def _stop(self):
        if not self._player:
            return
        self._player.stop()
        self._is_playing = False
        self.btn_play.configure(text="▶")
        self._seek_var.set(0)
        self.var_time.set("00:00:00 / 00:00:00")
        self._hide_subtitle()
        self._stop_update_loop()

    def _seek_relative(self, ms: int):
        if not self._player:
            return
        current = self._player.get_time()
        if current >= 0:
            self._player.set_time(max(0, current + ms))

    def _on_seek(self, val):
        if not self._player or not self._is_playing:
            return
        self._player.set_position(float(val) / 1000.0)

    def _on_volume(self, val):
        if self._player:
            self._player.audio_set_volume(int(float(val)))

    # ──────────────────────────────────────────────────────────────────
    # Custom subtitle rendering
    # ──────────────────────────────────────────────────────────────────

    def _get_subtitle_at(self, ms: int) -> str:
        """Return the subtitle text active at the given millisecond."""
        if not self._current_sub_label:
            return ""
        entries = self._srt_files.get(self._current_sub_label, [])
        for entry in entries:
            if entry["start"] <= ms <= entry["end"]:
                return entry["text"]
        return ""

    def _show_subtitle(self, text: str):
        """Display subtitle text in the chosen mode."""
        if text == self._last_sub_text:
            return
        self._last_sub_text = text

        if not text:
            self._hide_subtitle()
            return

        if self._sub_mode == MODE_OVERLAY:
            self._overlay_label.configure(text=text)
            self._overlay_label.place(relx=0.5, rely=self._overlay_pos_y, anchor="s")
        else:
            self._sub_panel_text.configure(text=text)

    def _hide_subtitle(self):
        self._last_sub_text = ""
        self._overlay_label.configure(text="")
        self._overlay_label.place_forget()
        self._sub_panel_text.configure(text="")

    # ──────────────────────────────────────────────────────────────────
    # Update loop
    # ──────────────────────────────────────────────────────────────────

    def _start_update_loop(self):
        self._stop_update_loop()
        self._tick()

    def _stop_update_loop(self):
        if self._update_id:
            self.after_cancel(self._update_id)
            self._update_id = None

    def _tick(self):
        if not self._player:
            return

        if self._player.is_playing():
            current_ms = self._player.get_time()
            total_ms = self._player.get_length()

            if total_ms > 0:
                self._seek_var.set((current_ms / total_ms) * 1000)
                self.var_time.set(
                    f"{self._format_time(current_ms)} / {self._format_time(total_ms)}"
                )

            # Render custom subtitle
            if current_ms >= 0:
                text = self._get_subtitle_at(current_ms)
                self._show_subtitle(text)

        self._update_id = self.after(100, self._tick)

    @staticmethod
    def _format_time(ms: int) -> str:
        if ms < 0:
            return "00:00:00"
        s = ms // 1000
        return f"{s // 3600:02d}:{(s % 3600) // 60:02d}:{s % 60:02d}"

    # ──────────────────────────────────────────────────────────────────
    # Cleanup
    # ──────────────────────────────────────────────────────────────────

    def release(self):
        self._stop_update_loop()
        if self._player:
            self._player.stop()
            self._player.release()
        if self._instance:
            self._instance.release()
