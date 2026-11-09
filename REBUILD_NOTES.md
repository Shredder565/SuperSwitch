# SuperSwitch rebuild notes

SuperSwitch is Neil's personal Switch emulator, built on Ryubing (Ryujinx fork, .NET 10, Vulkan, Avalonia).
The original repo (C:\Users\neil\Documents\SuperSwitch) was lost when the PC was reformatted on 2026-10-18.
This folder holds everything recovered, so all the work can be re-applied to fresh Ryubing source.

Fresh source: `git clone https://starforgejo.dev/archive/Ryubing.git` (mirror of git.ryujinx.app, which is down).
Ryubing's upstream config version was 73 when the original work started.

## Contents
- `recovered/`: exact code recovered from earlier review transcripts:
  - `GameRecorder.cs` (Ryujinx.Common/Recording, latest full version), `RecordingSettings.cs`, `RecordingCapture.cs` (Vulkan)
  - `StallProfiler.cs` (Ryujinx.Common), `show_1a8c6e2_1a78de9.diff` (parallel deswizzle + chunked ASTC decode)
  - `recorder_8e67231_ui_config.diff` (recorder settings UI/config), `apphost_*.txt`, `window_parts.txt`, `audio_tap.txt`
  - `agent-ad229f03f.txt`: full async shader diff (commit 7ba4a45) plus review findings; `agent-ae737de2f.txt`: full review of later commits
- `gpu-astc/src/...`: final versions of the files touched by GPU ASTC decoding (and earlier profiling changes in Texture.cs/TextureGroup.cs/AstcDecoder.cs)
- `shader/`: the ASTC compute shader (template + generated `astc.comp`), compiled `AstcDecode.spv`, table generator, CPU test harness

## Features to re-apply (in original order)
1. Rebrand: exe/assembly name SuperSwitch, own data folder (%AppData%\SuperSwitch), own NVIDIA profile, Ryubing updater disabled.
   After renaming the UI assembly, fix embedded resource lookup (EmbeddedResources mapping).
2. Async shader compilation (Vulkan) with a Settings > Graphics toggle (config v74: EnableAsyncShaderCompilation=true).
   Apply the review fixes: `using System.Threading`; spin only on first queue / per-frame budget; add pending entry before Task.Run
   (or separate key copy); wait for pending compiles before pipeline cache/layout disposal on shutdown; sweep completed pending variants;
   don't skip draws during occlusion queries; treat faulted compile task as link failure.
3. 4K defaults (config v75): ResScale 2x docked, 16x anisotropy, SMAA Ultra, docked mode; migrate existing configs once.
4. Game-only recorder (F7 toggle; F9 is docked/handheld): captures the game image before UI, raw RGBA to FFmpeg stdin,
   h264_nvenc/hevc_nvenc, 4K cap (3840x2160), self-paced 60 fps clock, audio s16le via named pipe tapped in HardwareDeviceImpl,
   codec/quality settings, instant replay buffer (F6 saves last 15s-2min, segment muxer). Config v77.
   Apply the full-review fixes (replay-save folder deletion, writer wait timeout, ExitCode after Kill, audio thread exceptions).
5. Stall profiler (periodic log + per-hitch breakdown for frames > 50 ms) with hooks in Vulkan, Gpu, ARMeilleure Translator, texture loads.
6. JIT warm-up: precompile emulator assemblies on background threads at startup (QuickJit and ReadyToRun are disabled in Ryubing's runtime config).
   Do NOT use ReadyToRun publish (fails fast with 0xc0000602).
7. Hide the console window by default (config v76 ShowConsole=false).
8. One-click launcher script (rebuild if needed, then start) + desktop shortcut creator. Build with `dotnet build`, not single-file publish.
9. Parallel block-linear deswizzle; chunked Parallel.For ASTC decode (inline if < 2048 blocks); ThreadPool min workers = 2x cores.
10. Slow texture load logging (first 60 loads > 1 ms).
11. Xbox controller defaults so A maps to A and B to B (swap A/B and X/Y vs Ryubing's Nintendo-layout defaults); config v78 migrates
    configs still on the old defaults.
12. GPU ASTC decoding (compute shader, embedded SPIR-V, self-check vs CPU decoder for first 24 decodes, env var SUPERSWITCH_GPU_ASTC=0 to disable).
    Record the decode on the main command buffer (not preload: global barrier batch), add image barriers around the copy,
    skip TextureBuffer and multisample targets. Shader validated bit-exact vs ARM astcenc (with spec rounding).

## Build notes
- LibHac NuGet feed was unreachable last time: cloned/built LibHac locally and used a local NuGet feed.
- FFmpeg must be on PATH (winget install Gyan.FFmpeg) or found by GameRecorder.FindFfmpeg.
13. Auto-record every game at startup (config v79): HEVC, "Efficient" quality = NVENC p5, tune hq, VBR CQ 25 (HEVC) / 22 (H.264),
    max 45M / 70M, spatial+temporal AQ, lookahead 10, 3 B-frames. (p6 + multipass was too slow for real-time 4K60.)
14. SuperSwitch Video Converter (tools/VideoConverter, WinForms): default preset "High quality, small files (recommended)",
    same settings but p6 + multipass qres + lookahead 20. Install-Video-Converter.cmd installs FFmpeg (winget Gyan.FFmpeg) if missing.
15. Shader cache backup/restore (config v80): restore before the render thread starts, back up after the game closes.
    Default folder: %OneDrive%\SuperSwitch Shader Cache Backup.
16. P pauses/resumes (config v81, was F5).
17. FS integrity checks off by default for faster loading (config v82).
18. NVIDIA driver profile ("SuperSwitch Nvidia Profile", SuperSwitch.exe): shader disk cache on (0x00198FFF = 1) and
    max size unlimited (0x00AC8497 = 0xFFFFFFFF); failures are only logged.
19. Synchronous pipeline creation logs its reason (first 40 > 1 ms). Every remaining one in Tropical Freeze was an occlusion
    query, so draws inside occlusion queries may now also compile in the background (config v83,
    Settings > Graphics > "Also during visibility checks"). Result: no shader-build stutter during play.
20. StallProfiler frame clock resets on a new session and on resume (no fake multi-second "hitches").
21. Every launch mirrors the whole source (minus build output) to %OneDrive%\SuperSwitch Source Backup\<computer name>, hidden in the background.
22. Data folder of your choice: %LocalAppData%\SuperSwitch\DataLocation.txt holds the folder for saves, settings, keys,
    firmware and caches (a DataLocation.txt next to SuperSwitch.exe overrides it; --root-data-dir overrides both).
    File > "Change Data Folder (saves, settings, caches)..." copies the data there (newer files win) and restarts.
23. Installer: Build-Setup.cmd builds SuperSwitch + the Video Converter and compiles installer\SuperSwitch.iss with Inno Setup 6
    (installed via winget if missing) into SuperSwitch-Setup.exe. The installer installs per user (default C:\SuperSwitch),
    asks for the data folder (default C:\SuperSwitch Data), can copy existing data, installs the .NET 10 desktop runtime
    and FFmpeg via winget when missing, and creates shortcuts.
24. Bug-fix pass (2026-11-09):
    - Recorder: a normal recording is written as `<name>.recording.mkv` and rewrapped (no re-encode, no time limit) to MP4 when it
      stops; a crash or power cut leaves a playable MKV instead of a broken MP4. FFmpeg is no longer killed 30 s after stopping.
      The recording clock stands still while emulation is paused (GameRecorder.SetPaused from AppHost.Pause/Resume).
      Leftover `replay-save-*` temp folders older than an hour are removed.
    - Shader cache backup: decisions use file times, not "bigger wins". Restore when the local cache is missing, or the backup is
      newer and not smaller. Back up only when something changed. Purging a game's shader cache also deletes its backup.
      A backup that could not be restored at startup is not overwritten that session. Old backup is kept as `.old` during the swap.
    - Launcher: "needs build?" compares against build-logs\last-successful-build.stamp instead of SuperSwitch.exe.
    - Data folder: if the chosen folder can't be reached at startup, a warning dialog says which fallback folder is in use.
    - Installer: reads DataLocation.txt as text (byte order mark safe); drive-root data folders are quoted correctly for robocopy.
    - F6/F7 are checked after the configurable hotkeys, so a hotkey assigned to F6/F7 in Settings wins.
    - Recorder audio: every audio stream the game plays is mixed into the recording (audio renderer sessions via HardwareDeviceImpl,
      direct audout via AudioDeviceSession.FlushToHardware). Before, only the first stream seen was recorded, so a game whose
      first stream is silent got a silent recording (Luigi's Mansion 2 HD). The log lists each stream and its peak level at the end.

## Restoring after a reformat
Copy the newest folder inside "SuperSwitch Source Backup" on OneDrive to Documents\SuperSwitch, install the .NET 10 SDK
(winget install Microsoft.DotNet.SDK.10), then run Create-Desktop-Shortcut.cmd and start SuperSwitch from the desktop icon.
The shader caches come back automatically from "SuperSwitch Shader Cache Backup".
