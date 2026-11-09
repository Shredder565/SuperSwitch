using System;

namespace Ryujinx.Graphics.GAL
{
    /// <summary>
    /// SuperSwitch: describes ASTC compressed texture data (in linear block order) to be decoded by the host GPU.
    /// The data layout matches the input of the CPU ASTC decoder: levels outer, layers inner.
    /// </summary>
    public readonly record struct AstcUploadInfo(
        int BlockWidth,
        int BlockHeight,
        int Width,
        int Height,
        int Depth,
        int Levels,
        int Layers);

    /// <summary>
    /// SuperSwitch: global switch for GPU accelerated ASTC decoding.
    /// </summary>
    public static class GpuAstcDecoding
    {
        private static volatile bool _isAvailable;

        /// <summary>
        /// True when the active renderer can decode ASTC textures on the GPU.
        /// Set by the backend once its decoder is ready, and cleared if its self-check fails.
        /// </summary>
        public static bool IsAvailable
        {
            get => _isAvailable && !DisabledByUser;
            set => _isAvailable = value;
        }

        /// <summary>
        /// Setting the environment variable SUPERSWITCH_GPU_ASTC=0 forces the CPU decoder.
        /// </summary>
        public static bool DisabledByUser { get; } = Environment.GetEnvironmentVariable("SUPERSWITCH_GPU_ASTC") == "0";
    }
}
