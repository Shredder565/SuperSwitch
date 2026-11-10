using System.Diagnostics;

namespace SuperSwitch.Graphics.Vulkan
{
    /// <summary>
    /// SuperSwitch: how the Vulkan backend handles draws whose shaders/pipelines are not compiled yet.
    /// </summary>
    public static class ShaderCompilationSettings
    {
        private static volatile bool _asyncCompilation = true;

        /// <summary>
        /// When true, draws that need a pipeline which is still compiling are skipped instead of
        /// stalling the render thread. Removes shader compilation stutter, at the cost of objects or
        /// effects possibly being missing for a few frames the first time they appear.
        /// When false, the emulator waits for every pipeline (Ryubing's original behaviour).
        /// </summary>
        public static bool AsyncCompilation
        {
            get => _asyncCompilation;
            set => _asyncCompilation = value;
        }

        private static volatile bool _asyncDuringOcclusionQueries = true;

        /// <summary>
        /// When true, draws inside an occlusion query (the game asking "is this object visible?") may also be skipped
        /// while their pipeline compiles. Removes the remaining shader stutter, at the cost of the game possibly getting a
        /// wrong visibility answer for a frame or two (a flare or object may flicker the first time it appears).
        /// </summary>
        public static bool AsyncDuringOcclusionQueries
        {
            get => _asyncDuringOcclusionQueries;
            set => _asyncDuringOcclusionQueries = value;
        }

        /// <summary>
        /// How long a draw may wait for a freshly queued pipeline variant before it is skipped.
        /// Most variants finish well within this, because the driver reuses already compiled shaders.
        /// </summary>
        internal static readonly long VariantWaitBudgetTicks = Stopwatch.Frequency / 1000; // 1 ms

        /// <summary>
        /// The most time one frame may spend on those short waits in total. Without a cap, a frame that meets many new
        /// pipeline variants at once (entering a new area) waited 1 ms for each of them and became a visible hitch.
        /// </summary>
        internal static readonly long FrameWaitBudgetTicks = Stopwatch.Frequency * 2 / 1000; // 2 ms

        // Render thread only.
        private static long _frameWaitSpentTicks;

        /// <summary>
        /// How long the next new variant may be waited for: the per-variant budget, or what is left of the frame's.
        /// </summary>
        internal static long TakeVariantWaitBudget()
        {
            long remaining = FrameWaitBudgetTicks - _frameWaitSpentTicks;

            return remaining <= 0 ? 0 : System.Math.Min(VariantWaitBudgetTicks, remaining);
        }

        internal static void AddVariantWait(long ticks)
        {
            _frameWaitSpentTicks += ticks;
        }

        /// <summary>
        /// Starts a new frame's wait budget. Called once per presented frame.
        /// </summary>
        public static void OnFramePresented()
        {
            _frameWaitSpentTicks = 0;
        }
    }
}
