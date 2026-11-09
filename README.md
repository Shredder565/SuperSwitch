# SuperSwitch

SuperSwitch is a personal Nintendo Switch emulator for Windows, built for a 4K display, an NVIDIA GPU and a controller.
It starts on a console-style home screen, records gameplay, and keeps its shader caches and source backed up.

SuperSwitch is based on Ryujinx as continued by the Ryubing project. The CPU, GPU and
system emulation come from that work, which is used under the MIT license (see `LICENSE.txt`). Third-party notices
are in `distribution/legal/THIRDPARTY.md`.

## What SuperSwitch adds

- **Console home screen**: every game as a large tile, most recently played first, with controller navigation.
  The classic library is one menu click away.
- **In-game quick menu**: Escape, or both sticks pressed in, pauses the game and offers Resume, recording,
  instant replay, full screen, Settings and Exit game.
- **Game recorder**: records the game image without the interface, in 4K at 60 fps, with all game audio,
  using NVIDIA NVENC through FFmpeg. Recording can start automatically with every game. An instant replay
  buffer saves the last moments on demand. A separate Video Converter shrinks finished recordings.
- **Smoother play**: shaders compile in the background instead of stalling the game, ASTC textures are decoded
  on the GPU, emulator code is pre-compiled at startup, and a stall profiler logs what caused any hitch.
- **Safety nets**: each game's shader cache is backed up and restored automatically, the data folder (saves,
  settings, keys, firmware, caches) can live anywhere, and the launcher mirrors the source to OneDrive.
- **Defaults for this setup**: 4K (2x docked resolution), 16x anisotropic filtering, SMAA Ultra, Xbox-style
  button layout, and a 0.50 stick deadzone.

## Controls

| Where | Input | Action |
| --- | --- | --- |
| Home screen | D-pad, either stick, arrow keys | Choose a game |
| Home screen | A, Enter, double-click | Play |
| Home screen | X | Game options |
| Home screen | + | Settings |
| In a game | Escape, or both sticks pressed in | Quick menu |
| In a game | P | Pause or resume |
| In a game | F7 | Start or stop recording |
| In a game | F6 | Save instant replay |
| Anywhere | F11 | Full screen |

## Requirements

- Windows 10 or 11, 64-bit
- An NVIDIA GPU with Vulkan (recording needs NVENC)
- The [.NET 10 SDK](https://dotnet.microsoft.com/download) to build: `winget install Microsoft.DotNet.SDK.10`
- FFmpeg for recording: `winget install Gyan.FFmpeg`
- Your own Switch keys, firmware and game dumps. SuperSwitch does not include any, and they must come from
  hardware and games you own.

## Build and run

Run `SuperSwitch.cmd`. It builds when the source has changed and then starts SuperSwitch.
`Create-Desktop-Shortcut.cmd` puts a shortcut to it on the desktop, and `Build-Setup.cmd` produces an installer
(`SuperSwitch-Setup.exe`). Build logs go to `build-logs`, and emulator logs to `build\Logs`.

`REBUILD_NOTES.md` is the running record of every SuperSwitch change and how the pieces fit together.

## License

SuperSwitch is released under the MIT license, the same license as Ryujinx. See `LICENSE.txt`.
