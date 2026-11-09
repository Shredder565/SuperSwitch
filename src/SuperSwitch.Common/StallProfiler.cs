using SuperSwitch.Common.Logging;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace SuperSwitch.Common
{
    /// <summary>
    /// SuperSwitch: lightweight accounting of time spent blocking the emulation/render threads,
    /// summarized in the log every few seconds. Used to find what causes remaining stutters.
    /// </summary>
    public static class StallProfiler
    {
        public enum Category
        {
            GuestShaderTranslation,
            HostProgramLinkWait,
            PipelineVariantWait,
            SyncPipelineCreate,
            ComputePipelineCreate,
            GuestSyncWait,
            BufferReadback,
            TextureReadback,
            GuestCodeTranslation,
            TextureUpload,
            TextureDeswizzle,
            TextureSetData,
            TextureFullSync,
            TexturePartialSync,
            TextureGroupCopy,
            TextureReprotect,
            TextureOverlapFlush,
            Count,
        }

        private static readonly string[] _names =
        [
            "Shader translation (GPU thread)",
            "Waiting for program link",
            "Pipeline variant grace wait",
            "Synchronous pipeline creation",
            "Compute pipeline creation",
            "Waiting for the GPU (guest sync)",
            "GPU->CPU buffer readback",
            "GPU->CPU texture readback",
            "Game CPU code translation (JIT)",
            "Texture upload/conversion (GPU thread)",
            "  of which: memory layout conversion",
            "  of which: upload to the host GPU",
            "  of which: full texture syncs",
            "  of which: partial texture syncs",
            "  of which: texture group copies",
            "  of which: memory re-protection",
            "  of which: overlap flushes",
        ];

        private const long ReportIntervalMs = 5000;
        private const double LongFrameMs = 25.0;

        private static readonly long[] _totalTicks = new long[(int)Category.Count];
        private static readonly long[] _maxTicks = new long[(int)Category.Count];
        private static readonly int[] _counts = new int[(int)Category.Count];

        // Per-frame accumulators, used to explain individual hitches.
        private static readonly long[] _frameTicks = new long[(int)Category.Count];
        private static readonly int[] _frameCounts = new int[(int)Category.Count];
        private static System.TimeSpan _frameGcPause = System.GC.GetTotalPauseDuration();
        private const double HitchReportMs = 50.0;

        private static long _lastReport = Stopwatch.GetTimestamp();
        private static long _lastFrame;
        private static long _lastHitchReport;
        private static int _frames;
        private static int _longFrames;
        private static double _worstFrameMs;
        private static System.TimeSpan _lastGcPause = System.GC.GetTotalPauseDuration();
        private static int _lastGen2 = System.GC.CollectionCount(2);
        private static int _lastGen0 = System.GC.CollectionCount(0);

        private static readonly long _minTicks = Stopwatch.Frequency / 4000; // 0.25 ms

        public static bool Enabled { get; set; } = true;

        public static long Begin() => Enabled ? Stopwatch.GetTimestamp() : 0;

        public static void End(Category category, long start)
        {
            if (start == 0)
            {
                return;
            }

            long elapsed = Stopwatch.GetTimestamp() - start;

            // Ignore anything too short to be noticeable, so hot paths can be measured unconditionally.
            if (elapsed < _minTicks)
            {
                return;
            }

            int index = (int)category;

            Interlocked.Add(ref _totalTicks[index], elapsed);
            Interlocked.Increment(ref _counts[index]);
            Interlocked.Add(ref _frameTicks[index], elapsed);
            Interlocked.Increment(ref _frameCounts[index]);

            long max;

            while (elapsed > (max = Volatile.Read(ref _maxTicks[index])))
            {
                if (Interlocked.CompareExchange(ref _maxTicks[index], elapsed, max) == max)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Forget the last frame time, so a new game session or resuming from pause isn't reported as one long frame.
        /// </summary>
        public static void ResetFrameClock() => _lastFrame = 0;

        /// <summary>
        /// Call once per presented frame (render thread).
        /// </summary>
        public static void OnFramePresented()
        {
            if (!Enabled)
            {
                return;
            }

            long now = Stopwatch.GetTimestamp();

            if (_lastFrame != 0)
            {
                double frameMs = (now - _lastFrame) * 1000.0 / Stopwatch.Frequency;

                _frames++;

                if (frameMs > LongFrameMs)
                {
                    _longFrames++;
                }

                if (frameMs > _worstFrameMs)
                {
                    _worstFrameMs = frameMs;
                }

                if (frameMs > HitchReportMs && (now - _lastHitchReport) * 1000 / Stopwatch.Frequency >= 1000)
                {
                    _lastHitchReport = now;

                    ReportHitch(frameMs);
                }
            }

            // Start a fresh per-frame window.
            for (int i = 0; i < (int)Category.Count; i++)
            {
                Interlocked.Exchange(ref _frameTicks[i], 0);
                Interlocked.Exchange(ref _frameCounts[i], 0);
            }

            _frameGcPause = System.GC.GetTotalPauseDuration();

            _lastFrame = now;

            if ((now - _lastReport) * 1000 / Stopwatch.Frequency < ReportIntervalMs)
            {
                return;
            }

            _lastReport = now;

            StringBuilder sb = null;

            for (int i = 0; i < (int)Category.Count; i++)
            {
                int count = Interlocked.Exchange(ref _counts[i], 0);
                long total = Interlocked.Exchange(ref _totalTicks[i], 0);
                long max = Interlocked.Exchange(ref _maxTicks[i], 0);

                if (count == 0)
                {
                    continue;
                }

                sb ??= new StringBuilder();
                sb.Append($"\n    {_names[i]}: {count}x, total {ToMs(total):F1} ms, worst {ToMs(max):F1} ms");
            }

            System.TimeSpan gcPause = System.GC.GetTotalPauseDuration();
            int gen0 = System.GC.CollectionCount(0);
            int gen2 = System.GC.CollectionCount(2);
            double gcPauseMs = (gcPause - _lastGcPause).TotalMilliseconds;

            if (gcPauseMs >= 1.0)
            {
                sb ??= new StringBuilder();
                sb.Append($"\n    .NET garbage collection pauses: total {gcPauseMs:F1} ms ({gen0 - _lastGen0} collections, {gen2 - _lastGen2} full)");
            }

            _lastGcPause = gcPause;
            _lastGen0 = gen0;
            _lastGen2 = gen2;

            if (sb != null || _longFrames > 0)
            {
                Logger.Info?.Print(LogClass.Gpu,
                    $"Stall report (last {ReportIntervalMs / 1000}s): {_frames} frames, {_longFrames} over {LongFrameMs} ms, worst frame {_worstFrameMs:F1} ms{sb}",
                    "StallProfiler");
            }

            _frames = 0;
            _longFrames = 0;
            _worstFrameMs = 0;
        }

        private static void ReportHitch(double frameMs)
        {
            StringBuilder sb = new();

            for (int i = 0; i < (int)Category.Count; i++)
            {
                long ticks = Volatile.Read(ref _frameTicks[i]);

                if (ticks > 0)
                {
                    sb.Append($"; {_names[i]} {ToMs(ticks):F1} ms ({Volatile.Read(ref _frameCounts[i])}x)");
                }
            }

            double gcMs = (System.GC.GetTotalPauseDuration() - _frameGcPause).TotalMilliseconds;

            if (gcMs >= 0.5)
            {
                sb.Append($"; GC pauses {gcMs:F1} ms");
            }

            if (sb.Length == 0)
            {
                sb.Append("; nothing measured (game loading, guest CPU work or OS/driver)");
            }

            Logger.Info?.Print(LogClass.Gpu, $"Hitch: frame took {frameMs:F0} ms{sb}", "StallProfiler");
        }

        private static double ToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    }
}
