using System;

namespace SuperSwitch.Graphics.GAL
{
    public interface IImageArray : IDisposable
    {
        void SetImages(int index, ITexture[] images);
    }
}
