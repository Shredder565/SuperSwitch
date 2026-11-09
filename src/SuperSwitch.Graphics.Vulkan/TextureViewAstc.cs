using SuperSwitch.Common.Logging;
using SuperSwitch.Common.Memory;
using SuperSwitch.Graphics.GAL;
using SuperSwitch.Graphics.Texture.Astc;
using Silk.NET.Vulkan;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace SuperSwitch.Graphics.Vulkan
{
    /// <summary>
    /// SuperSwitch: ASTC uploads decoded by a compute shader instead of the CPU.
    /// </summary>
    partial class TextureView
    {
        // Number of GPU decodes compared against the CPU decoder at the start of each session.
        private const int AstcSelfCheckCount = 6;

        private static int _astcSelfChecksStarted;
        private static int _astcSelfChecksPassed;
        private static long _astcGpuDecodes;
        private static long _astcCpuFallbacks;

        /// <inheritdoc/>
        public bool TrySetDataAstc(MemoryOwner<byte> data, AstcUploadInfo info, int layer, int level, bool singleSlice)
        {
            try
            {
                bool decodedOnGpu = false;

                try
                {
                    decodedOnGpu = TryDecodeAstcOnGpu(data.Memory, info, layer, level, singleSlice);
                }
                catch (Exception ex)
                {
                    GpuAstcDecoding.IsAvailable = false;

                    Logger.Error?.Print(LogClass.Gpu, $"GPU ASTC decoding failed and has been disabled for this session: {ex}");
                }

                if (!decodedOnGpu)
                {
                    Interlocked.Increment(ref _astcCpuFallbacks);

                    DecodeAstcOnCpuAndUpload(data.Memory, info, layer, level, singleSlice);
                }
            }
            finally
            {
                data.Dispose();
            }

            return true;
        }

        private void DecodeAstcOnCpuAndUpload(ReadOnlyMemory<byte> data, AstcUploadInfo info, int layer, int level, bool singleSlice)
        {
            if (!AstcDecoder.TryDecodeToRgba8P(
                data,
                info.BlockWidth,
                info.BlockHeight,
                info.Width,
                info.Height,
                info.Depth,
                info.Levels,
                info.Layers,
                out MemoryOwner<byte> decoded))
            {
                Logger.Debug?.Print(LogClass.Gpu, $"Invalid ASTC texture ({info.Width}x{info.Height} {info.BlockWidth}x{info.BlockHeight}).");
            }

            using (decoded)
            {
                UploadDecodedAstc(decoded.Span, layer, level, singleSlice);
            }
        }

        private void UploadDecodedAstc(ReadOnlySpan<byte> decoded, int layer, int level, bool singleSlice)
        {
            if (singleSlice)
            {
                SetData(decoded, layer, level, 1, 1, singleSlice: true);
            }
            else
            {
                SetData(decoded, 0, 0, Info.GetLayers(), Info.Levels, singleSlice: false);
            }
        }

        private bool TryDecodeAstcOnGpu(ReadOnlyMemory<byte> data, AstcUploadInfo info, int layer, int level, bool singleSlice)
        {
            if (!GpuAstcDecoding.IsAvailable || !_gd.HelperShader.IsAstcDecodeReady)
            {
                return false;
            }

            // Only plain RGBA8 2D/array/cube targets, which is what the host texture of an ASTC texture is.
            if (Info.Target == Target.Texture3D || info.Depth != 1 || Info.BytesPerPixel != 4 || Info.BlockWidth != 1 || Info.BlockHeight != 1)
            {
                return false;
            }

            if ((uint)(info.BlockWidth - 4) > 8 || (uint)(info.BlockHeight - 4) > 8 || NeedsD24S8Conversion())
            {
                return false;
            }

            int expectedLevels = singleSlice ? 1 : Info.Levels;
            int expectedLayers = singleSlice ? 1 : Info.GetLayers();
            int baseLevel = singleSlice ? level : 0;

            if (info.Levels != expectedLevels ||
                info.Layers != expectedLayers ||
                info.Width != Math.Max(1, Info.Width >> baseLevel) ||
                info.Height != Math.Max(1, Info.Height >> baseLevel))
            {
                return false;
            }

            // Level/layer table, in the same order as the CPU decoder (levels outer, layers inner).
            int entryCount = info.Levels * info.Layers;
            int[] levelInfo = new int[entryCount * 8];

            int totalBlocks = 0;
            int outputTexels = 0;

            for (int l = 0; l < info.Levels; l++)
            {
                int sizeX = Math.Max(1, info.Width >> l);
                int sizeY = Math.Max(1, info.Height >> l);
                int sizeZ = Math.Max(1, info.Depth >> l);

                int blocksX = (sizeX + info.BlockWidth - 1) / info.BlockWidth;
                int blocksY = (sizeY + info.BlockHeight - 1) / info.BlockHeight;
                int blocks = blocksX * blocksY * sizeZ;

                for (int a = 0; a < info.Layers; a++)
                {
                    int e = (l * info.Layers + a) * 8;

                    levelInfo[e + 0] = totalBlocks;
                    levelInfo[e + 1] = blocksX;
                    levelInfo[e + 2] = blocks;
                    levelInfo[e + 3] = sizeX;
                    levelInfo[e + 4] = sizeY * sizeZ;
                    levelInfo[e + 5] = outputTexels;

                    totalBlocks += blocks;
                    outputTexels += sizeX * sizeY * sizeZ;
                }
            }

            int inputSize = totalBlocks * 16;
            int outputSize = outputTexels * 4;
            int levelInfoSize = entryCount * 8 * sizeof(int);

            if (totalBlocks == 0 || data.Length < inputSize)
            {
                return false;
            }

            using BufferHolder input = _gd.BufferManager.Create(_gd, inputSize);
            using BufferHolder levels = _gd.BufferManager.Create(_gd, levelInfoSize);
            using BufferHolder output = _gd.BufferManager.Create(_gd, outputSize, baseType: BufferAllocationType.DeviceLocal);

            if (input == null || levels == null || output == null)
            {
                return false;
            }

            data.Span[..inputSize].CopyTo(input.GetDataStorage(0, inputSize));
            MemoryMarshal.AsBytes(levelInfo.AsSpan()).CopyTo(levels.GetDataStorage(0, levelInfoSize));

            Auto<DisposableImage> imageAuto = GetImage();

            // Recorded on the main command buffer (like the other helper shader operations), so the batched
            // pipeline barriers are flushed into the right command buffer and the pipeline state is restored after.
            _gd.PipelineInternal.EndRenderPass();

            CommandBufferScoped cbs = _gd.PipelineInternal.CurrentCommandBuffer;

            _gd.HelperShader.DecodeAstc(_gd, cbs, input, levels, output, outputSize, info.BlockWidth, info.BlockHeight, totalBlocks, entryCount);

            VkBuffer outputBuffer = output.GetBuffer(cbs.CommandBuffer).Get(cbs, 0, outputSize).Value;
            Image image = imageAuto.Get(cbs).Value;

            int barrierLayer = singleSlice ? layer : 0;
            int barrierLevel = singleSlice ? level : 0;
            int barrierLayers = singleSlice ? 1 : Info.GetLayers();
            int barrierLevels = singleSlice ? 1 : Info.Levels;

            InsertImageBarrier(
                _gd.Api,
                cbs.CommandBuffer,
                image,
                TextureStorage.DefaultAccessMask,
                AccessFlags.TransferWriteBit,
                PipelineStageFlags.AllCommandsBit,
                PipelineStageFlags.TransferBit,
                Info.Format.ConvertAspectFlags(),
                FirstLayer + barrierLayer,
                FirstLevel + barrierLevel,
                barrierLayers,
                barrierLevels);

            if (singleSlice)
            {
                CopyFromOrToBuffer(cbs.CommandBuffer, outputBuffer, image, outputSize, false, layer, level, 1, 1, true);
            }
            else
            {
                CopyFromOrToBuffer(cbs.CommandBuffer, outputBuffer, image, outputSize, false, 0, 0, Info.GetLayers(), Info.Levels, false);
            }

            InsertImageBarrier(
                _gd.Api,
                cbs.CommandBuffer,
                image,
                AccessFlags.TransferWriteBit,
                TextureStorage.DefaultAccessMask,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.AllCommandsBit,
                Info.Format.ConvertAspectFlags(),
                FirstLayer + barrierLayer,
                FirstLevel + barrierLevel,
                barrierLayers,
                barrierLevels);

            long decodes = Interlocked.Increment(ref _astcGpuDecodes);

            if (decodes == 1)
            {
                Logger.Info?.Print(LogClass.Gpu, $"First ASTC texture decoded on the GPU ({info.Width}x{info.Height}, {info.BlockWidth}x{info.BlockHeight} blocks).");
            }

            // Self-check: compare the first few GPU decodes against the CPU decoder.
            if ((Info.Target == Target.Texture2D || singleSlice) &&
                Volatile.Read(ref _astcSelfChecksStarted) < AstcSelfCheckCount &&
                Interlocked.Increment(ref _astcSelfChecksStarted) <= AstcSelfCheckCount)
            {
                RunAstcSelfCheck(data, info, layer, level, singleSlice);
            }

            return true;
        }

        private void RunAstcSelfCheck(ReadOnlyMemory<byte> data, AstcUploadInfo info, int layer, int level, bool singleSlice)
        {
            AstcDecoder.TryDecodeToRgba8P(
                data,
                info.BlockWidth,
                info.BlockHeight,
                info.Width,
                info.Height,
                info.Depth,
                info.Levels,
                info.Layers,
                out MemoryOwner<byte> cpuDecoded);

            using (cpuDecoded)
            {
                ReadOnlySpan<byte> expected = cpuDecoded.Span;

                int length;
                int mismatches = 0;
                int maxDiff = 0;
                int firstMismatch = -1;
                int gpuLength;

                using (PinnedSpan<byte> gpuData = singleSlice ? GetData(layer, level) : GetData())
                {
                    ReadOnlySpan<byte> actual = gpuData.Get();

                    gpuLength = actual.Length;
                    length = Math.Min(expected.Length, actual.Length);

                    for (int i = 0; i < length; i++)
                    {
                        int diff = Math.Abs(expected[i] - actual[i]);

                        if (diff != 0)
                        {
                            if (firstMismatch < 0)
                            {
                                firstMismatch = i;
                            }

                            mismatches++;
                            maxDiff = Math.Max(maxDiff, diff);
                        }
                    }
                }

                string desc = $"{info.Width}x{info.Height} {info.BlockWidth}x{info.BlockHeight} levels {info.Levels} layers {info.Layers}";

                if (mismatches == 0 && gpuLength >= expected.Length)
                {
                    int passed = Interlocked.Increment(ref _astcSelfChecksPassed);

                    Logger.Info?.Print(LogClass.Gpu, $"GPU ASTC self-check passed ({passed}): {desc}, bit-identical to the CPU decoder.");

                    return;
                }

                Logger.Warning?.Print(LogClass.Gpu,
                    $"GPU ASTC self-check MISMATCH: {desc}, {mismatches} of {length} bytes differ (max diff {maxDiff}, first at byte {firstMismatch}), " +
                    $"readback {gpuLength} bytes vs expected {expected.Length}.");

                // Small rounding differences are tolerated, anything else turns GPU decoding off for the session.
                if (maxDiff > 1 || mismatches > length / 100 || gpuLength < expected.Length)
                {
                    GpuAstcDecoding.IsAvailable = false;

                    Logger.Warning?.Print(LogClass.Gpu, "GPU ASTC decoding has been disabled for this session; using the CPU decoder.");
                }

                // Replace the texture contents with the CPU result.
                UploadDecodedAstc(expected, layer, level, singleSlice);
            }
        }
    }
}
