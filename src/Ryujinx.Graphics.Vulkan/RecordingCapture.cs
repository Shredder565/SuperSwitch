using Ryujinx.Common.Logging;
using Ryujinx.Common.Recording;
using Ryujinx.Graphics.GAL;
using Silk.NET.Vulkan;
using System;
using System.Threading;
using Format = Ryujinx.Graphics.GAL.Format;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Ryujinx.Graphics.Vulkan
{
    /// <summary>
    /// SuperSwitch: copies the presented game image (before it is drawn into the window, so without any UI)
    /// into host memory every frame, asynchronously, and hands it to the <see cref="GameRecorder"/>.
    /// </summary>
    class RecordingCapture : IDisposable
    {
        private const int SlotCount = 4;

        private const int SlotFree = 0;
        private const int SlotGpuPending = 1;
        private const int SlotWithEncoder = 2;

        private sealed class Slot
        {
            public BufferHolder Buffer;
            public int State;
            public Action Release;
        }

        private readonly VulkanRenderer _gd;
        private readonly Slot[] _slots;
        private readonly TextureView _target;
        private readonly int _size;
        private int _nextSlot;

        public GameRecorder Recorder { get; }
        public int Width { get; }
        public int Height { get; }

        public RecordingCapture(VulkanRenderer gd, GameRecorder recorder, int width, int height)
        {
            _gd = gd;
            Recorder = recorder;

            // Video encoders need even dimensions.
            Width = Math.Max(2, width & ~1);
            Height = Math.Max(2, height & ~1);

            _size = Width * Height * 4;

            TextureCreateInfo info = new(
                Width,
                Height,
                1,
                1,
                1,
                1,
                1,
                4,
                Format.R8G8B8A8Unorm,
                DepthStencilMode.Depth,
                Target.Texture2D,
                SwizzleComponent.Red,
                SwizzleComponent.Green,
                SwizzleComponent.Blue,
                SwizzleComponent.Alpha);

            _target = (TextureView)gd.CreateTexture(info);

            _slots = new Slot[SlotCount];

            for (int i = 0; i < SlotCount; i++)
            {
                Slot slot = new()
                {
                    Buffer = gd.BufferManager.Create(gd, _size, baseType: BufferAllocationType.HostMapped),
                };

                slot.Release = () => Volatile.Write(ref slot.State, SlotFree);

                _slots[i] = slot;
            }

            Logger.Info?.Print(LogClass.Gpu, $"Recording capture started at {Width}x{Height}.");
        }

        /// <summary>
        /// Hands finished GPU copies to the encoder. Call once per present, on the render thread.
        /// </summary>
        public void Poll()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                int index = (_nextSlot + i) % SlotCount;
                Slot slot = _slots[index];

                if (Volatile.Read(ref slot.State) != SlotGpuPending || !slot.Buffer.IsGpuIdle())
                {
                    continue;
                }

                nint data = slot.Buffer.MappedPointer;

                if (data == 0)
                {
                    continue;
                }

                Volatile.Write(ref slot.State, SlotWithEncoder);

                if (!Recorder.TrySubmitVideoFrame(data, Width, Height, slot.Release))
                {
                    Volatile.Write(ref slot.State, SlotFree);
                }
            }
        }

        /// <summary>
        /// Records commands that copy the game image into a free slot. Frames are dropped if no slot is free.
        /// </summary>
        public void Capture(CommandBufferScoped cbs, TextureView view, int srcX0, int srcY0, int srcX1, int srcY1, bool flipX, bool flipY)
        {
            Slot slot = null;

            for (int i = 0; i < SlotCount; i++)
            {
                int index = (_nextSlot + i) % SlotCount;

                if (Volatile.Read(ref _slots[index].State) == SlotFree)
                {
                    slot = _slots[index];
                    _nextSlot = (index + 1) % SlotCount;
                    break;
                }
            }

            if (slot == null)
            {
                // Encoder or GPU is behind; skip this frame (FFmpeg repeats the previous one).
                return;
            }

            // Make previous transfers from the target finish before we draw into it again.
            FullBarrier(cbs);

            // Same orientation rules as the swapchain blit in Window.Present.
            int dstX0 = flipX ? Width : 0;
            int dstX1 = flipX ? 0 : Width;
            int dstY0 = flipY ? 0 : Height;
            int dstY1 = flipY ? Height : 0;

            _gd.HelperShader.BlitColor(
                _gd,
                cbs,
                view,
                _target,
                new Extents2D(srcX0, srcY0, srcX1, srcY1),
                new Extents2D(dstX0, dstY1, dstX1, dstY0),
                true,
                true);

            FullBarrier(cbs);

            VkBuffer buffer = slot.Buffer.GetBuffer(cbs.CommandBuffer, true).Get(cbs).Value;
            Image image = _target.GetImage().Get(cbs).Value;

            _target.CopyFromOrToBuffer(cbs.CommandBuffer, buffer, image, _size, true, 0, 0, 1, 1, true);

            FullBarrier(cbs, AccessFlags.HostReadBit, PipelineStageFlags.HostBit);

            Volatile.Write(ref slot.State, SlotGpuPending);
        }

        private void FullBarrier(CommandBufferScoped cbs, AccessFlags extraDstAccess = 0, PipelineStageFlags extraDstStage = 0)
        {
            MemoryBarrier barrier = new()
            {
                SType = StructureType.MemoryBarrier,
                SrcAccessMask = AccessFlags.MemoryWriteBit,
                DstAccessMask = AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit | extraDstAccess,
            };

            unsafe
            {
                _gd.Api.CmdPipelineBarrier(
                    cbs.CommandBuffer,
                    PipelineStageFlags.AllCommandsBit,
                    PipelineStageFlags.AllCommandsBit | extraDstStage,
                    0,
                    1,
                    in barrier,
                    0,
                    null,
                    0,
                    null);
            }
        }

        public void Dispose()
        {
            // The encoder thread may still be reading slot memory; wait until it has let go of everything.
            // GPU-side, disposal is deferred by reference counting until in-flight command buffers complete.
            if (!Recorder.WaitForWriter(10000))
            {
                // FFmpeg is stuck. Leak the buffers rather than free memory the writer may still read.
                Logger.Warning?.Print(LogClass.Gpu, "Recording writer did not finish; leaking capture buffers.");
                _target.Release();
                return;
            }

            foreach (Slot slot in _slots)
            {
                slot.Buffer.Dispose();
            }

            _target.Release();

            Logger.Info?.Print(LogClass.Gpu, "Recording capture stopped.");
        }
    }
}
