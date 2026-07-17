@echo off
setlocal enabledelayedexpansion

REM ═══════════════════════════════════════════════════════════════════
REM  Video Enhancement Script — Audio + Video
REM  - Audio: noise reduction (afftdn) + volume normalization (loudnorm)
REM  - Video: stabilization (vidstab) + sharpening + brightness/contrast
REM ═══════════════════════════════════════════════════════════════════

set "INPUT=%~1"
if "%INPUT%"=="" (
    echo Usage: enhance-video.bat "path\to\video.mp4"
    exit /b 1
)

REM Derive output filename
set "DIR=%~dp1"
set "NAME=%~n1"
set "EXT=%~x1"
set "OUTPUT_NAME=%NAME%_enhanced%EXT%"
set "TEMP_OUT=_enhance_temp_output%EXT%"

REM Work from the input file's directory to avoid path issues in FFmpeg filters
cd /d "%DIR%"

echo.
echo ===========================================================
echo  VIDEO ENHANCEMENT
echo ===========================================================
echo  Input:  %INPUT%
echo  Output: %DIR%%OUTPUT_NAME%
echo ===========================================================
echo.

REM ── Pass 1: Detect motion for stabilization ──────────────────────
echo [1/2] Analyzing video for stabilization...
ffmpeg -y -i "%NAME%%EXT%" -vf vidstabdetect=stepsize=6:shakiness=8:accuracy=9:result=transforms.trf -an -f null NUL
if errorlevel 1 (
    echo [WARNING] Stabilization detection failed. Proceeding without stabilization...
    goto :PASS2_NO_STAB
)

REM ── Pass 2: Apply all enhancements (use temp output to avoid space issues) ──
echo.
echo [2/2] Applying enhancements (stabilization + sharpening + color + audio)...
ffmpeg -y -i "%NAME%%EXT%" -vf "vidstabtransform=input=transforms.trf:zoom=1:smoothing=10:interpol=bicubic,deblock=filter=strong:block=4,unsharp=7:7:1.2:5:5:0.6,eq=brightness=-0.08:contrast=1.15:saturation=1.2:gamma=0.85" -af "highpass=f=200,afftdn=nf=-20:nr=30:nt=w,equalizer=f=250:t=q:w=1.5:g=-8,equalizer=f=2500:t=q:w=2:g=8,equalizer=f=4000:t=q:w=1.5:g=5,acompressor=threshold=-25dB:ratio=8:attack=3:release=50:makeup=10dB,speechnorm=p=0.95:l=1,loudnorm=I=-12:TP=-1:LRA=5" -c:v libx264 -preset slow -crf 18 -c:a aac -b:a 192k %TEMP_OUT%
goto :RENAME

:PASS2_NO_STAB
echo.
echo [2/2] Applying enhancements (sharpening + color + audio, no stabilization)...
ffmpeg -y -i "%NAME%%EXT%" -vf "deblock=filter=strong:block=4,unsharp=7:7:1.2:5:5:0.6,eq=brightness=-0.08:contrast=1.15:saturation=1.2:gamma=0.85" -af "highpass=f=200,afftdn=nf=-20:nr=30:nt=w,equalizer=f=250:t=q:w=1.5:g=-8,equalizer=f=2500:t=q:w=2:g=8,equalizer=f=4000:t=q:w=1.5:g=5,acompressor=threshold=-25dB:ratio=8:attack=3:release=50:makeup=10dB,speechnorm=p=0.95:l=1,loudnorm=I=-12:TP=-1:LRA=5" -c:v libx264 -preset slow -crf 18 -c:a aac -b:a 192k %TEMP_OUT%

:RENAME
if errorlevel 1 (
    echo.
    echo [ERROR] Enhancement failed!
    if exist %TEMP_OUT% del %TEMP_OUT%
    exit /b 1
)

REM Rename temp output to final name
if exist "%OUTPUT_NAME%" del "%OUTPUT_NAME%"
move %TEMP_OUT% "%OUTPUT_NAME%" >nul

REM Cleanup temp file
if exist transforms.trf del transforms.trf

echo.
echo ===========================================================
echo  DONE! Enhanced video saved to:
echo  %DIR%%OUTPUT_NAME%
echo ===========================================================
echo.
