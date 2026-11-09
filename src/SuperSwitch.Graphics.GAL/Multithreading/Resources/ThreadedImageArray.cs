using SuperSwitch.Graphics.GAL.Multithreading.Commands.ImageArray;
using SuperSwitch.Graphics.GAL.Multithreading.Model;

namespace SuperSwitch.Graphics.GAL.Multithreading.Resources
{
    /// <summary>
    /// Threaded representation of a image array.
    /// </summary>
    class ThreadedImageArray : IImageArray
    {
        private readonly ThreadedRenderer _renderer;
        public IImageArray Base;

        public ThreadedImageArray(ThreadedRenderer renderer)
        {
            _renderer = renderer;
        }

        private TableRef<T> Ref<T>(T reference)
        {
            return new TableRef<T>(_renderer, reference);
        }

        public unsafe void Dispose()
        {
            _renderer.New<ImageArrayDisposeCommand>()->Set(Ref(this));
            _renderer.QueueCommand();
        }

        public unsafe void SetImages(int index, ITexture[] images)
        {
            _renderer.New<ImageArraySetImagesCommand>()->Set(Ref(this), index, Ref(images));
            _renderer.QueueCommand();
        }
    }
}
