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
    }
}
