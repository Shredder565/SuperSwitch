using SuperSwitch.Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SuperSwitch.Common.Recording
{
    /// <summary>
    /// SuperSwitch game recorder. Receives raw RGBA frames of the game image (no emulator UI) from the
    /// graphics backend and the game's audio from the audio backend, and encodes them to MP4 with FFmpeg
    /// (NVIDIA NVENC for video, AAC for audio). A recording is written as Matroska and rewrapped as MP4 when it stops.
    /// </summary>
    /// <remarks>
    /// Both streams are paced against one wall clock started with the encoder: video is written at a constant
    /// 60 fps (repeating the latest game frame as needed) and audio is written at 48 kHz (all of the game's
    /// audio streams mixed, gaps filled with silence), so they stay in sync no matter how the game's frame rate varies.
    /// </remarks>
    public sealed class GameRecorder
    {
        private const int FrameRate = 60;
        private const int SampleRate = 48000;
        private const int AudioChannels = 2;
        private const int MaxQueuedFrames = 3;

        // Audio mixing, in sample frames.
        private const int AudioMixChunk = SampleRate / 100;          // Mix in steps of at least 10 ms.
        private const int AudioMixMax = SampleRate / 10;             // ...and at most 100 ms at a time.
        private const long AudioPrimeFrames = SampleRate * 4 / 100;  // A source starts playing once it has 40 ms queued (absorbs jitter).
        private const long AudioMaxBacklog = SampleRate * 3 / 10;    // A source more than 300 ms ahead is trimmed back, to stay in sync.

        private static readonly object _lock = new();
        private static volatile GameRecorder _current;

        /// <summary>
        /// The active recording, or null when not recording.
        /// </summary>
        public static GameRecorder Current => _current;

        /// <summary>
        /// True while a manual recording (not the instant replay buffer) is running.
        /// </summary>
        public static bool IsRecording => _current is { IsReplayBuffer: false };

        /// <summary>
        /// True while the instant replay buffer is running.
        /// </summary>
        public static bool IsReplayBufferRunning => _current is { IsReplayBuffer: true };

        private const int ReplaySegmentSeconds = 2;

        /// <summary>
        /// Folder with the rolling replay segments, or null for a normal recording.
        /// </summary>
        private readonly string _replayDir;
        private readonly RecordingCodec _codec;
        private readonly RecordingQuality _quality;
        private readonly int _replaySeconds;
        private readonly int _segmentWrap;
        private int _savesInProgress;

        public bool IsReplayBuffer => _replayDir != null;

        /// <summary>
        /// Raised when recording starts or stops. Arguments: is recording, user-facing message.
        /// </summary>
        public static event Action<bool, string> StateChanged;

        public string OutputPath { get; }

        /// <summary>
        /// A normal recording is first written here (Matroska), then converted to <see cref="OutputPath"/> when it stops.
        /// Unlike an MP4, a Matroska file stays playable if SuperSwitch or the PC stops unexpectedly mid-recording.
        /// </summary>
        private readonly string _tempPath;

        private readonly string _ffmpegPath;
        private readonly BlockingCollection<VideoFrame> _queue = new(MaxQueuedFrames);
        private readonly ManualResetEventSlim _writerExited = new(false);
        private readonly CancellationTokenSource _stopToken = new();

        private Process _process;
        private Stream _videoInput;
        private NamedPipeServerStream _audioPipe;
        private Thread _writerThread;
        private Thread _audioThread;
        private int _width;
        private int _height;
        private long _clockStart;
        private long _pausedAt;
        private readonly object _clockLock = new();
        private static volatile bool _pauseRequested;
        private volatile bool _encoderStarted;
        private volatile bool _failed;
        private long _framesWritten;
        private long _framesSkipped;
        private long _framesDropped;
        private long _finalVideoFrames = -1;

        // Every audio stream the game plays (it can have several at once) is queued separately and mixed by the audio writer.
        private readonly object _audioLock = new();
        private readonly Dictionary<object, AudioSource> _audioSources = new();
        private bool _audioFormatWarned;

        private sealed class AudioSource
        {
            public string Name;
            public readonly Queue<short[]> Chunks = new();
            public int HeadOffset;     // Samples already used from the first queued chunk.
            public long QueuedFrames;
            public bool Primed;
            public long TotalFrames;
            public int Peak;

            public void Enqueue(short[] stereo, int frames, int peak)
            {
                Chunks.Enqueue(stereo);
                QueuedFrames += frames;
                TotalFrames += frames;

                if (peak > Peak)
                {
                    Peak = peak;
                }

                if (QueuedFrames > AudioMaxBacklog)
                {
                    // Far ahead of the recording clock (burst at start, or the clock skipped): drop the oldest audio.
                    while (QueuedFrames > AudioPrimeFrames && Chunks.Count > 1)
                    {
                        short[] dropped = Chunks.Dequeue();

                        QueuedFrames -= (dropped.Length - HeadOffset) / AudioChannels;
                        HeadOffset = 0;
                    }
                }
            }

            /// <summary>
            /// Adds up to <paramref name="frames"/> frames of this source to the mix; what it doesn't have stays silent.
            /// </summary>
            public void MixInto(int[] mix, int frames, bool flush)
            {
                if (!Primed)
                {
                    if (QueuedFrames < AudioPrimeFrames && !flush)
                    {
                        return;
                    }

                    Primed = true;
                }

                int done = 0;

                while (done < frames && Chunks.Count > 0)
                {
                    short[] head = Chunks.Peek();
                    int take = Math.Min((head.Length - HeadOffset) / AudioChannels, frames - done);
                    int samples = take * AudioChannels;
                    int mixOffset = done * AudioChannels;

                    for (int i = 0; i < samples; i++)
                    {
                        mix[mixOffset + i] += head[HeadOffset + i];
                    }

                    HeadOffset += samples;
                    QueuedFrames -= take;
                    done += take;

                    if (HeadOffset >= head.Length)
                    {
                        Chunks.Dequeue();
                        HeadOffset = 0;
                    }
                }

                if (done < frames)
                {
                    // Ran dry (the game stopped this stream or is running slow): build up a little buffer again first.
                    Primed = false;
                }
            }
        }

        private readonly struct VideoFrame
        {
            public readonly nint Data;
            public readonly int Length;
            public readonly Action Release;

            public VideoFrame(nint data, int length, Action release)
            {
                Data = data;
                Length = length;
                Release = release;
            }
        }

        private GameRecorder(string outputPath, string ffmpegPath, string replayDir = null)
        {
            OutputPath = outputPath;
            _tempPath = outputPath == null ? null : Path.ChangeExtension(outputPath, null) + ".recording.mkv";
            _ffmpegPath = ffmpegPath;
            _replayDir = replayDir;
            _codec = RecordingSettings.Codec;
            _quality = RecordingSettings.Quality;
            _replaySeconds = RecordingSettings.ReplayBufferSeconds;

            // Generous spare segments, so saving (copying) never races with FFmpeg overwriting the oldest one.
            _segmentWrap = (_replaySeconds + ReplaySegmentSeconds - 1) / ReplaySegmentSeconds + 10;
        }

        /// <summary>
        /// Starts the instant replay buffer: the game is encoded continuously into short rolling segments,
        /// and <see cref="SaveReplay"/> turns the most recent ones into an MP4.
        /// </summary>
        public static bool StartReplayBuffer(out string error)
        {
            lock (_lock)
            {
                if (_current != null)
                {
                    error = "A recording is already in progress.";
                    return false;
                }

                string ffmpeg = FindFfmpeg();

                if (ffmpeg == null)
                {
                    error = "FFmpeg was not found. Install it (for example: winget install Gyan.FFmpeg) or put ffmpeg.exe next to SuperSwitch.exe.";
                    return false;
                }

                // A fresh folder per session: a previous session's FFmpeg may still be finishing in the old one.
                string dir = Path.Combine(TempRoot, $"replay-{Guid.NewGuid():N}");

                try
                {
                    if (Directory.Exists(TempRoot))
                    {
                        foreach (string stale in Directory.GetDirectories(TempRoot, "replay-*"))
                        {
                            // Never touch a replay that is being saved right now, or a folder still in use.
                            // Save folders only live for seconds, so one older than an hour was left behind by a crash.
                            TimeSpan age = DateTime.UtcNow - Directory.GetLastWriteTimeUtc(stale);
                            bool isSaveFolder = Path.GetFileName(stale).StartsWith("replay-save-");

                            if (age < (isSaveFolder ? TimeSpan.FromHours(1) : TimeSpan.FromMinutes(5)))
                            {
                                continue;
                            }

                            TryDeleteDirectory(stale);
                        }
                    }

                    Directory.CreateDirectory(dir);
                }
                catch (Exception e)
                {
                    error = $"Could not prepare the replay folder: {e.Message}";
                    return false;
                }

                _current = new GameRecorder(null, ffmpeg, dir);
                error = null;
            }

            Logger.Info?.Print(LogClass.Application, $"Instant replay buffer started ({RecordingSettings.ReplayBufferSeconds} s).", "Recorder");

            return true;
        }

        /// <summary>
        /// Saves the last <see cref="RecordingSettings.ReplayBufferSeconds"/> seconds of the replay buffer to an MP4, in the background.
        /// </summary>
        public static bool SaveReplay(string outputPath, out string error)
        {
            GameRecorder recorder = _current;

            if (recorder is not { IsReplayBuffer: true })
            {
                error = "The instant replay buffer is not running. Enable it in Settings > Graphics.";
                return false;
            }

            if (recorder._failed)
            {
                error = "The instant replay buffer stopped because of an encoder error. Check the log for details.";
                return false;
            }

            if (!recorder._encoderStarted)
            {
                error = "Nothing has been captured yet.";
                return false;
            }

            Interlocked.Increment(ref recorder._savesInProgress);

            // All file work happens in the background, so the game and UI never wait on the disk.
            Task.Run(() =>
            {
                string message;

                try
                {
                    message = recorder.SaveReplayFiles(outputPath);
                }
                catch (Exception e)
                {
                    message = $"Recording failed: could not save the replay ({e.Message}).";
                }
                finally
                {
                    Interlocked.Decrement(ref recorder._savesInProgress);
                }

                Logger.Notice.Print(LogClass.Application, message, "Recorder");
                StateChanged?.Invoke(IsRecording, message);
            });

            error = null;
            return true;
        }

        private string SaveReplayFiles(string outputPath)
        {
            string snapshotDir = Path.Combine(TempRoot, $"replay-save-{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(snapshotDir);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                string[] segments = GetNewestSegments((_replaySeconds + ReplaySegmentSeconds - 1) / ReplaySegmentSeconds + 1);

                if (segments.Length == 0)
                {
                    return "Recording failed: nothing has been captured yet.";
                }

                // Copy first: the encoder keeps overwriting the oldest segments. Allow sharing, since the newest is still being written.
                string[] copies = new string[segments.Length];

                for (int i = 0; i < segments.Length; i++)
                {
                    copies[i] = Path.Combine(snapshotDir, $"{i:D3}.ts");

                    using FileStream source = new(segments[i], FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using FileStream destination = new(copies[i], FileMode.Create, FileAccess.Write);
                    source.CopyTo(destination);
                }

                string tag = _codec == RecordingCodec.Hevc ? "-tag:v hvc1 " : string.Empty;
                string args =
                    "-hide_banner -loglevel warning -y " +
                    $"-i \"concat:{string.Join('|', copies)}\" " +
                    $"-c copy {tag}-bsf:a aac_adtstoasc -movflags +faststart \"{outputPath}\"";

                using Process process = Process.Start(new ProcessStartInfo(_ffmpegPath, args)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                })!;

                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Logger.Warning?.Print(LogClass.Application, $"ffmpeg (replay save): {stderr}", "Recorder");
                    return $"Recording failed: could not save the replay (FFmpeg exit code {process.ExitCode}).";
                }

                return $"Replay saved: {outputPath}";
            }
            finally
            {
                TryDeleteDirectory(snapshotDir);
            }
        }

        /// <summary>
        /// Returns the paths of the newest replay segments, oldest first: the finished ones from FFmpeg's segment list,
        /// plus the one currently being written (its truncated end is harmless for MPEG-TS).
        /// </summary>
        private string[] GetNewestSegments(int count)
        {
            string listPath = Path.Combine(_replayDir, "segments.csv");
            List<string> finished = new();

            if (File.Exists(listPath))
            {
                using FileStream stream = new(listPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using StreamReader reader = new(stream);

                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    int comma = line.IndexOf(',');
                    string name = comma >= 0 ? line[..comma] : line;

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        finished.Add(name.Trim().Trim('"'));
                    }
                }
            }

            List<string> result = new();

            if (finished.Count > 0)
            {
                // The segment after the last finished one is the one being written now.
                string last = finished[^1];
                string digits = new(last.Where(char.IsDigit).ToArray());

                if (int.TryParse(digits, out int index))
                {
                    string current = Path.Combine(_replayDir, $"seg{(index + 1) % _segmentWrap:D3}.ts");

                    if (File.Exists(current) && File.GetLastWriteTimeUtc(current) >= File.GetLastWriteTimeUtc(Path.Combine(_replayDir, last)))
                    {
                        result.Add(current);
                    }
                }

                foreach (string name in Enumerable.Reverse(finished))
                {
                    if (result.Count >= count)
                    {
                        break;
                    }

                    string path = Path.Combine(_replayDir, name);

                    if (File.Exists(path) && !result.Contains(path))
                    {
                        result.Add(path);
                    }
                }

                result.Reverse();
            }
            else
            {
                // Nothing finished yet (less than one segment recorded): use what is there.
                result.AddRange(Directory.GetFiles(_replayDir, "seg*.ts"));
            }

            return result.ToArray();
        }

        /// <summary>
        /// Tells the recorder that emulation is paused (or resumed). While paused, the recording clock stands still,
        /// so no repeated frames or silence are written and the video continues seamlessly on resume.
        /// </summary>
        public static void SetPaused(bool paused)
        {
            _pauseRequested = paused;
        }

        /// <summary>
        /// Recording time elapsed since the encoder started, in Stopwatch ticks, not counting time spent paused.
        /// Shared by the video and audio writers so both follow exactly the same timeline.
        /// </summary>
        private long ClockElapsed(out bool paused)
        {
            lock (_clockLock)
            {
                long now = Stopwatch.GetTimestamp();

                paused = _pauseRequested;

                if (paused && _pausedAt == 0)
                {
                    _pausedAt = now;
                }
                else if (!paused && _pausedAt != 0)
                {
                    _clockStart += now - _pausedAt;
                    _pausedAt = 0;
                }

                return (_pausedAt != 0 ? _pausedAt : now) - _clockStart;
            }
        }

        private void AdvanceClock(long ticks)
        {
            lock (_clockLock)
            {
                _clockStart += ticks;
            }
        }

        private static string TempRoot => Path.Combine(Path.GetTempPath(), "SuperSwitch");

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception)
            {
                // Temporary files; not important.
            }
        }

        /// <summary>
        /// Finds ffmpeg.exe: next to the emulator, in an "ffmpeg" folder next to it, or on the PATH.
        /// </summary>
        public static string FindFfmpeg()
        {
            string exeName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string[] candidates =
            [
                Path.Combine(baseDir, exeName),
                Path.Combine(baseDir, "ffmpeg", exeName),
                Path.Combine(baseDir, "ffmpeg", "bin", exeName),
            ];

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            foreach (string dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim('"'), exeName);

                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // Invalid PATH entry.
                }
            }

            // winget installs (Gyan.FFmpeg) put a link here, which may not be on PATH for already running apps.
            if (OperatingSystem.IsWindows())
            {
                string links = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", exeName);

                if (File.Exists(links))
                {
                    return links;
                }
            }

            return null;
        }

        /// <summary>
        /// Starts a new recording. The encoder starts once the first game frame arrives.
        /// </summary>
        public static bool Start(string outputPath, out string error)
        {
            lock (_lock)
            {
                if (_current != null)
                {
                    error = "A recording is already in progress.";
                    return false;
                }

                string ffmpeg = FindFfmpeg();

                if (ffmpeg == null)
                {
                    error = "FFmpeg was not found. Install it (for example: winget install Gyan.FFmpeg) or put ffmpeg.exe next to SuperSwitch.exe.";
                    return false;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                }
                catch (Exception e)
                {
                    error = $"Could not create the recordings folder: {e.Message}";
                    return false;
                }

                _current = new GameRecorder(outputPath, ffmpeg);
                error = null;
            }

            Logger.Notice.Print(LogClass.Application, $"Recording started: {outputPath}", "Recorder");
            StateChanged?.Invoke(true, $"Recording to {outputPath}");

            return true;
        }

        /// <summary>
        /// Stops the current recording. The file is finalized in the background.
        /// </summary>
        public static void Stop()
        {
            StopRecorder(null);
        }

        /// <summary>
        /// Stops the given recorder if it is still the current one (null stops whatever is current).
        /// </summary>
        private static void StopRecorder(GameRecorder expected)
        {
            GameRecorder recorder;

            lock (_lock)
            {
                if (expected != null && _current != expected)
                {
                    return;
                }

                recorder = _current;
                _current = null;

                // Inside the lock: the encoder is only ever started under it after checking this,
                // so no new FFmpeg can start for a recorder that is stopping.
                recorder?._queue.CompleteAdding();
            }

            if (recorder == null)
            {
                return;
            }

            recorder._stopToken.Cancel();

            Thread finisher = new(() => recorder.Finish())
            {
                Name = "GameRecorder.Finish",
                IsBackground = false,
            };

            finisher.Start();
        }

        /// <summary>
        /// Blocks until the video writer has let go of all submitted frames. Call before freeing frame memory.
        /// </summary>
        /// <returns>True if the writer let go of all frames within the timeout</returns>
        public bool WaitForWriter(int timeoutMs)
        {
            lock (_lock)
            {
                // If no frame ever arrived, the writer was never started and holds nothing.
                if (_writerThread == null)
                {
                    return true;
                }
            }

            return _writerExited.Wait(timeoutMs);
        }

        /// <summary>
        /// Submits a frame of tightly packed RGBA8 pixels. The memory must stay valid until <paramref name="release"/> is called.
        /// </summary>
        /// <returns>False if the frame was not accepted; the caller keeps ownership and must not wait for release</returns>
        public bool TrySubmitVideoFrame(nint data, int width, int height, Action release)
        {
            if (_failed || _queue.IsAddingCompleted)
            {
                return false;
            }

            if (!_encoderStarted)
            {
                lock (_lock)
                {
                    if (_queue.IsAddingCompleted || !StartEncoder(width, height))
                    {
                        return false;
                    }
                }
            }
            else if (width != _width || height != _height)
            {
                // The encoder can't change size mid-stream; the capture side always scales to the initial size.
                return false;
            }

            try
            {
                if (_queue.TryAdd(new VideoFrame(data, width * height * 4, release)))
                {
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                // Adding completed concurrently (recording stopped).
                return false;
            }

            Interlocked.Increment(ref _framesDropped);

            return false;
        }

        /// <summary>
        /// Submits game audio as interleaved signed 16-bit PCM. Called by the audio backend for every buffer it plays.
        /// </summary>
        /// <param name="source">Identifies the audio stream; all streams are mixed together in the recording</param>
        public void SubmitAudio(object source, ReadOnlySpan<short> samples, int channelCount, int sampleRate)
        {
            if (!_encoderStarted || _failed || _stopToken.IsCancellationRequested || samples.IsEmpty)
            {
                return;
            }

            if (sampleRate != SampleRate || (channelCount != 1 && channelCount != 2 && channelCount != 6))
            {
                if (!_audioFormatWarned)
                {
                    _audioFormatWarned = true;
                    Logger.Warning?.Print(LogClass.Application, $"Unsupported audio format for recording: {channelCount} channels at {sampleRate} Hz.", "Recorder");
                }

                return;
            }

            int frames = samples.Length / channelCount;
            short[] stereo = new short[frames * AudioChannels];

            switch (channelCount)
            {
                case 1:
                    for (int i = 0; i < frames; i++)
                    {
                        stereo[i * 2] = samples[i];
                        stereo[i * 2 + 1] = samples[i];
                    }

                    break;
                case 2:
                    samples[..(frames * 2)].CopyTo(stereo);
                    break;
                case 6:
                    // 5.1 (FL, FR, FC, LFE, BL, BR) to stereo.
                    for (int i = 0; i < frames; i++)
                    {
                        int b = i * 6;
                        float center = samples[b + 2] * 0.7071f;
                        float left = samples[b] + center + samples[b + 4] * 0.7071f;
                        float right = samples[b + 1] + center + samples[b + 5] * 0.7071f;

                        stereo[i * 2] = (short)Math.Clamp(left, short.MinValue, short.MaxValue);
                        stereo[i * 2 + 1] = (short)Math.Clamp(right, short.MinValue, short.MaxValue);
                    }

                    break;
            }

            int peak = 0;

            for (int i = 0; i < stereo.Length; i++)
            {
                int magnitude = Math.Abs((int)stereo[i]);

                if (magnitude > peak)
                {
                    peak = magnitude;
                }
            }

            string newSourceName = null;

            lock (_audioLock)
            {
                if (!_audioSources.TryGetValue(source, out AudioSource audioSource))
                {
                    audioSource = new AudioSource { Name = $"{source.GetType().Name} #{_audioSources.Count + 1}" };
                    _audioSources.Add(source, audioSource);
                    newSourceName = audioSource.Name;
                }

                audioSource.Enqueue(stereo, frames, peak);
            }

            if (newSourceName != null)
            {
                Logger.Info?.Print(LogClass.Application, $"Recording audio stream {newSourceName} ({channelCount} channels).", "Recorder");
            }
        }

        private string GetVideoCodecArgs()
        {
            if (_quality == RecordingQuality.Efficient)
            {
                // Visually high quality 4K at a fraction of the size: a slower preset, two-pass rate control at quarter
                // resolution, spatial and temporal adaptive quantization, lookahead and B-frames. HEVC compresses
                // about twice as well as H.264, so it gets a higher constant-quality value for the same image quality.
                string efficientCodec = _codec == RecordingCodec.Hevc
                    ? "-c:v hevc_nvenc -profile:v main -cq 25 -maxrate 45M -bufsize 90M "
                    : "-c:v h264_nvenc -profile:v high -cq 22 -maxrate 70M -bufsize 140M ";

                // Real-time 4K60: p6 with two-pass encoding fell behind (frames had to be repeated), so this uses
                // the p5 preset without multipass; adaptive quantization and lookahead are cheap and keep detail.
                return efficientCodec +
                       "-preset p5 -tune hq -rc vbr -b:v 0 -spatial-aq 1 -temporal-aq 1 -aq-strength 8 " +
                       "-rc-lookahead 10 -bf 3 -b_ref_mode middle ";
            }

            (int cq, int maxRateMbps) = _quality switch
            {
                RecordingQuality.Balanced => (23, 80),
                RecordingQuality.Small => (28, 40),
                _ => (18, 160),
            };

            string codec = _codec == RecordingCodec.Hevc
                ? "-c:v hevc_nvenc -profile:v main "
                : "-c:v h264_nvenc -profile:v high ";

            return codec + $"-preset p5 -tune hq -rc vbr -cq {cq} -b:v 0 -maxrate {maxRateMbps}M -bufsize {maxRateMbps * 2}M ";
        }

        private string GetOutputArgs()
        {
            if (IsReplayBuffer)
            {
                int gop = FrameRate * ReplaySegmentSeconds;

                // Keyframe (IDR) at every segment boundary so segments can be joined without re-encoding.
                return $"-g {gop} -forced-idr 1 -force_key_frames \"expr:gte(t,n_forced*{ReplaySegmentSeconds})\" " +
                       $"-f segment -segment_format mpegts -segment_time {ReplaySegmentSeconds} -segment_wrap {_segmentWrap} " +
                       $"-segment_list \"{Path.Combine(_replayDir, "segments.csv")}\" -segment_list_type csv -segment_list_size {_segmentWrap} " +
                       $"\"{Path.Combine(_replayDir, "seg%03d.ts")}\"";
            }

            // Matroska while recording (see _tempPath); Finish() converts it to MP4 without re-encoding.
            return $"-f matroska \"{_tempPath}\"";
        }

        private bool StartEncoder(int width, int height)
        {
            if (_encoderStarted)
            {
                return true;
            }

            _width = width;
            _height = height;

            string pipeName = $"superswitch_audio_{Environment.ProcessId}_{Guid.NewGuid():N}";

            string args =
                "-hide_banner -loglevel warning -y " +
                $"-f rawvideo -pix_fmt rgba -video_size {width}x{height} -framerate {FrameRate} -thread_queue_size 64 -i pipe:0 " +
                $"-f s16le -ar {SampleRate} -ac {AudioChannels} -thread_queue_size 4096 -i \"\\\\.\\pipe\\{pipeName}\" " +
                "-map 0:v -map 1:a " +
                GetVideoCodecArgs() +
                "-c:a aac -b:a 256k " +
                GetOutputArgs();

            try
            {
                _audioPipe = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 1 << 20);

                ProcessStartInfo startInfo = new(_ffmpegPath, args)
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };

                _process = Process.Start(startInfo)!;
                _process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        Logger.Warning?.Print(LogClass.Application, $"ffmpeg: {e.Data}", "Recorder");
                    }
                };
                _process.OutputDataReceived += (_, _) => { };
                _process.BeginErrorReadLine();
                _process.BeginOutputReadLine();

                _videoInput = _process.StandardInput.BaseStream;
            }
            catch (Exception e)
            {
                _audioPipe?.Dispose();
                _audioPipe = null;

                Fail($"Failed to start FFmpeg: {e.Message}");

                return false;
            }

            Logger.Info?.Print(LogClass.Application,
                $"Encoding {width}x{height} @ {FrameRate} fps ({_codec}, {_quality}) with AAC audio to {(IsReplayBuffer ? "the instant replay buffer" : OutputPath)}",
                "Recorder");

            _clockStart = Stopwatch.GetTimestamp();

            _writerThread = new Thread(VideoWriterLoop)
            {
                Name = "GameRecorder.Video",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
            };

            _audioThread = new Thread(AudioWriterLoop)
            {
                Name = "GameRecorder.Audio",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
            };

            _encoderStarted = true;

            _writerThread.Start();
            _audioThread.Start();

            return true;
        }

        private unsafe void VideoWriterLoop()
        {
            long period = Stopwatch.Frequency / FrameRate;
            long frameIndex = 0;
            VideoFrame current = default;
            bool hasCurrent = false;

            try
            {
                while (true)
                {
                    // Always write the newest game frame; older queued ones are released unused.
                    while (_queue.TryTake(out VideoFrame frame))
                    {
                        if (hasCurrent)
                        {
                            current.Release();
                        }

                        current = frame;
                        hasCurrent = true;
                    }

                    if (_queue.IsAddingCompleted)
                    {
                        break;
                    }

                    if (!hasCurrent)
                    {
                        if (_queue.TryTake(out VideoFrame first, 50))
                        {
                            current = first;
                            hasCurrent = true;
                        }

                        continue;
                    }

                    long elapsed = ClockElapsed(out bool paused);
                    long dueElapsed = frameIndex * period;

                    if (elapsed < dueElapsed)
                    {
                        if (paused)
                        {
                            // The clock stands still while emulation is paused; just wait for resume (or stop).
                            Thread.Sleep(5);
                        }
                        else
                        {
                            WaitUntil(Stopwatch.GetTimestamp() + Math.Min(dueElapsed - elapsed, period));
                        }

                        continue; // Pick up any newer frame that arrived while waiting.
                    }

                    if (elapsed - dueElapsed > period * FrameRate / 2)
                    {
                        // More than half a second behind (e.g. the disk stalled). Skip ahead rather than burst-write.
                        // Move the shared clock forward instead of jumping the frame count, so the audio
                        // timeline (which follows the same clock) stays aligned with the video.
                        long skipFrames = (elapsed - dueElapsed) / period;
                        _framesSkipped += skipFrames;
                        AdvanceClock(skipFrames * period);
                    }

                    if (!_failed)
                    {
                        try
                        {
                            _videoInput.Write(new ReadOnlySpan<byte>((void*)current.Data, current.Length));
                        }
                        catch (IOException e)
                        {
                            Fail($"The encoder stopped unexpectedly: {e.Message}");
                        }
                    }

                    frameIndex++;
                    _framesWritten++;
                }
            }
            finally
            {
                if (hasCurrent)
                {
                    current.Release();
                }

                while (_queue.TryTake(out VideoFrame leftover))
                {
                    leftover.Release();
                }

                Interlocked.Exchange(ref _finalVideoFrames, frameIndex);

                _writerExited.Set();
            }
        }

        private void AudioWriterLoop()
        {
            int[] mix = new int[AudioMixMax * AudioChannels];
            byte[] output = new byte[AudioMixMax * AudioChannels * sizeof(short)];
            long written = 0;

            try
            {
                _audioPipe.WaitForConnectionAsync(_stopToken.Token).GetAwaiter().GetResult();
            }
            catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException)
            {
                return;
            }

            try
            {
                while (true)
                {
                    long finalVideoFrames = Interlocked.Read(ref _finalVideoFrames);
                    bool finishing = finalVideoFrames >= 0;
                    long target;

                    if (finishing)
                    {
                        // Video has finished: make the audio track exactly as long as the video, then stop.
                        target = finalVideoFrames * SampleRate / FrameRate;

                        if (written >= target)
                        {
                            break;
                        }
                    }
                    else
                    {
                        target = ClockElapsed(out _) * SampleRate / Stopwatch.Frequency;
                    }

                    // The audio track follows the recording clock exactly: write what is due, mixing every stream
                    // the game is playing. A stream with nothing queued (silent, paused, running slow) adds silence.
                    long due = target - written;

                    if (!finishing && due < AudioMixChunk)
                    {
                        Thread.Sleep(2);

                        continue;
                    }

                    int frames = (int)Math.Min(due, AudioMixMax);
                    int sampleCount = frames * AudioChannels;

                    Array.Clear(mix, 0, sampleCount);

                    lock (_audioLock)
                    {
                        foreach (AudioSource audioSource in _audioSources.Values)
                        {
                            audioSource.MixInto(mix, frames, finishing);
                        }
                    }

                    Span<short> outputSamples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, short>(output.AsSpan());

                    for (int i = 0; i < sampleCount; i++)
                    {
                        outputSamples[i] = (short)Math.Clamp(mix[i], short.MinValue, short.MaxValue);
                    }

                    _audioPipe.Write(output, 0, sampleCount * sizeof(short));
                    written += frames;
                }
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException or OperationCanceledException)
            {
                if (!_stopToken.IsCancellationRequested)
                {
                    Fail($"The audio encoder input closed unexpectedly: {e.Message}");
                }
            }
        }

        /// <summary>
        /// One line per audio stream that was recorded, for the log: how much audio it delivered and how loud it got.
        /// </summary>
        private string DescribeAudioSources()
        {
            lock (_audioLock)
            {
                if (_audioSources.Count == 0)
                {
                    return "no audio was received from the game";
                }

                List<string> parts = new();

                foreach (AudioSource audioSource in _audioSources.Values)
                {
                    string level = audioSource.Peak == 0
                        ? "silent"
                        : $"peak {20.0 * Math.Log10(audioSource.Peak / 32768.0):F0} dB";

                    parts.Add($"{audioSource.Name}: {(double)audioSource.TotalFrames / SampleRate:F1} s, {level}");
                }

                return string.Join("; ", parts);
            }
        }

        private static void WaitUntil(long timestamp)
        {
            long remaining;

            while ((remaining = timestamp - Stopwatch.GetTimestamp()) > 0)
            {
                if (remaining > Stopwatch.Frequency / 500) // > 2 ms
                {
                    Thread.Sleep(1);
                }
                else
                {
                    Thread.Yield();
                }
            }
        }

        private void Fail(string message)
        {
            if (_failed)
            {
                return;
            }

            _failed = true;

            Logger.Error?.Print(LogClass.Application, message, "Recorder");

            StateChanged?.Invoke(IsRecording, $"Recording error: {message} Check the log for FFmpeg's output.");

            // Don't leave a broken recorder in place (F6 would keep "saving" stale footage).
            StopRecorder(this);
        }

        private void Finish()
        {
            // Pairs with StartEncoder, which runs under the same lock: see a fully started (or never started) encoder.
            lock (_lock)
            {
            }

            if (_writerThread != null && !_writerExited.Wait(15000))
            {
                // FFmpeg stopped reading; kill it so the writer unblocks and the app can exit.
                Logger.Warning?.Print(LogClass.Application, "The video writer is stuck; stopping FFmpeg.", "Recorder");

                try
                {
                    _process?.Kill();
                }
                catch (Exception)
                {
                }

                _writerExited.Wait(5000);
            }

            _audioThread?.Join(10000);

            try
            {
                _videoInput?.Close();
            }
            catch (Exception e)
            {
                Logger.Warning?.Print(LogClass.Application, $"Error closing the video input: {e.Message}", "Recorder");
            }

            try
            {
                _audioPipe?.Dispose();
            }
            catch (Exception e)
            {
                Logger.Warning?.Print(LogClass.Application, $"Error closing the audio pipe: {e.Message}", "Recorder");
            }

            try
            {
                // Closing the inputs makes FFmpeg flush its encoder and finish the file; on a slow or busy disk
                // that can take a while, so be generous before giving up on it.
                if (_process != null && !_process.WaitForExit(180000))
                {
                    Logger.Warning?.Print(LogClass.Application, "FFmpeg did not finish in time; stopping it.", "Recorder");
                    _process.Kill();
                    _process.WaitForExit(5000);
                }
            }
            catch (Exception e)
            {
                Logger.Warning?.Print(LogClass.Application, $"Error while finishing the recording: {e.Message}", "Recorder");
            }

            if (IsReplayBuffer)
            {
                // Let any replay being saved finish copying its segments, then remove the rolling buffer files.
                for (int i = 0; i < 600 && Volatile.Read(ref _savesInProgress) > 0; i++)
                {
                    Thread.Sleep(50);
                }

                TryDeleteDirectory(_replayDir);

                Logger.Info?.Print(LogClass.Application, "Instant replay buffer stopped.", "Recorder");
                _process?.Dispose();

                return;
            }

            string message;

            if (_process == null)
            {
                message = "Recording stopped (no frames were captured).";
            }
            else if (_failed || !_process.HasExited || _process.ExitCode != 0)
            {
                int exitCode = _process.HasExited ? _process.ExitCode : -1;

                message = $"Recording failed (FFmpeg exit code {exitCode}). Check the log for details.";

                if (TempFileHasData())
                {
                    message += $" What was recorded up to that point is in {_tempPath}";
                }
            }
            else
            {
                double seconds = (double)_finalVideoFrames / FrameRate;

                message = ConvertToMp4()
                    ? $"Recording saved: {OutputPath} ({seconds:F0} s)."
                    : $"Recording saved as {_tempPath} ({seconds:F0} s). It could not be converted to MP4; check the log for details.";

                Logger.Info?.Print(LogClass.Application,
                    $"Recording stats: {_framesWritten} frames written at {FrameRate} fps, {_framesSkipped} skipped (writer behind), {_framesDropped} game frames not captured (encoder busy).",
                    "Recorder");
            }

            if (_process != null)
            {
                Logger.Info?.Print(LogClass.Application, $"Recording audio: {DescribeAudioSources()}.", "Recorder");
            }

            Logger.Notice.Print(LogClass.Application, message, "Recorder");
            StateChanged?.Invoke(false, message);

            _process?.Dispose();
        }

        private bool TempFileHasData()
        {
            try
            {
                return _tempPath != null && File.Exists(_tempPath) && new FileInfo(_tempPath).Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Rewraps the finished Matroska recording as MP4 (no re-encoding) and removes the Matroska file.
        /// There is deliberately no time limit: for a long recording this copies many gigabytes.
        /// On failure the Matroska file is kept, so the recording is never lost.
        /// </summary>
        private bool ConvertToMp4()
        {
            try
            {
                string tag = _codec == RecordingCodec.Hevc ? "-tag:v hvc1 " : string.Empty;
                string args = $"-hide_banner -loglevel warning -y -i \"{_tempPath}\" -map 0 -c copy {tag}\"{OutputPath}\"";

                int exitCode;
                string stderr;

                using (Process process = Process.Start(new ProcessStartInfo(_ffmpegPath, args)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                })!)
                {
                    stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }

                if (exitCode == 0 && File.Exists(OutputPath) && new FileInfo(OutputPath).Length > 0)
                {
                    File.Delete(_tempPath);

                    return true;
                }

                Logger.Warning?.Print(LogClass.Application, $"ffmpeg (convert to MP4, exit code {exitCode}): {stderr}", "Recorder");
            }
            catch (Exception e)
            {
                Logger.Warning?.Print(LogClass.Application, $"Could not convert the recording to MP4: {e.Message}", "Recorder");
            }

            try
            {
                // Don't leave a broken MP4 next to the good Matroska file.
                if (TempFileHasData() && File.Exists(OutputPath))
                {
                    File.Delete(OutputPath);
                }
            }
            catch (Exception)
            {
            }

            return false;
        }
    }
}
