using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL.Multithreading.Model;
using Ryujinx.Graphics.GAL.Multithreading.Resources;

namespace Ryujinx.Graphics.GAL.Multithreading.Commands.Texture
{
    struct TextureSetDataAstcCommand : IGALCommand, IGALCommand<TextureSetDataAstcCommand>
    {
        public readonly CommandType CommandType => CommandType.TextureSetDataAstc;
        private TableRef<ThreadedTexture> _texture;
        private TableRef<MemoryOwner<byte>> _data;
        private AstcUploadInfo _info;
        private int _layer;
        private int _level;
        private bool _singleSlice;

        public void Set(TableRef<ThreadedTexture> texture, TableRef<MemoryOwner<byte>> data, AstcUploadInfo info, int layer, int level, bool singleSlice)
        {
            _texture = texture;
            _data = data;
            _info = info;
            _layer = layer;
            _level = level;
            _singleSlice = singleSlice;
        }

        public static void Run(ref TextureSetDataAstcCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            ThreadedTexture texture = command._texture.Get(threaded);
            MemoryOwner<byte> data = command._data.Get(threaded);

            if (!texture.Base.TrySetDataAstc(data, command._info, command._layer, command._level, command._singleSlice))
            {
                // Should not happen, the flag is only set by backends that implement GPU decoding.
                Logger.Error?.Print(LogClass.Gpu, "GPU ASTC upload was rejected by the backend; texture data was dropped.");

                data.Dispose();
            }
        }
    }
}
