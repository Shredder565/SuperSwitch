using Silk.NET.Vulkan;
using System;
using Extent2D = SuperSwitch.Graphics.GAL.Extents2D;

namespace SuperSwitch.Graphics.Vulkan.Effects
{
    internal interface IScalingFilter : IDisposable
    {
        float Level { get; set; }
        void Run(
            TextureView view,
            CommandBufferScoped cbs,
            Auto<DisposableImageView> destinationTexture,
            Format format,
            int width,
            int height,
            Extent2D source,
            Extent2D destination);
    }
}
