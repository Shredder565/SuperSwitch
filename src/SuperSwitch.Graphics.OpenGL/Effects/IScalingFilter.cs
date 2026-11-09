using SuperSwitch.Graphics.GAL;
using SuperSwitch.Graphics.OpenGL.Image;
using System;

namespace SuperSwitch.Graphics.OpenGL.Effects
{
    internal interface IScalingFilter : IDisposable
    {
        float Level { get; set; }
        void Run(
            TextureView view,
            TextureView destinationTexture,
            int width,
            int height,
            Extents2D source,
            Extents2D destination);
    }
}
