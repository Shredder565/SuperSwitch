using SuperSwitch.Graphics.GAL.Multithreading.Model;
using SuperSwitch.Graphics.GAL.Multithreading.Resources;

namespace SuperSwitch.Graphics.GAL.Multithreading.Commands.TextureArray
{
    struct TextureArrayDisposeCommand : IGALCommand, IGALCommand<TextureArrayDisposeCommand>
    {
        public readonly CommandType CommandType => CommandType.TextureArrayDispose;
        private TableRef<ThreadedTextureArray> _textureArray;

        public void Set(TableRef<ThreadedTextureArray> textureArray)
        {
            _textureArray = textureArray;
        }

        public static void Run(ref TextureArrayDisposeCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            command._textureArray.Get(threaded).Base.Dispose();
        }
    }
}
