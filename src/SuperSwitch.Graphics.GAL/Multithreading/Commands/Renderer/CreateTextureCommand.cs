using SuperSwitch.Graphics.GAL.Multithreading.Model;
using SuperSwitch.Graphics.GAL.Multithreading.Resources;

namespace SuperSwitch.Graphics.GAL.Multithreading.Commands.Renderer
{
    struct CreateTextureCommand : IGALCommand, IGALCommand<CreateTextureCommand>
    {
        public readonly CommandType CommandType => CommandType.CreateTexture;
        private TableRef<ThreadedTexture> _texture;
        private TextureCreateInfo _info;

        public void Set(TableRef<ThreadedTexture> texture, TextureCreateInfo info)
        {
            _texture = texture;
            _info = info;
        }

        public static void Run(ref CreateTextureCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            command._texture.Get(threaded).Base = renderer.CreateTexture(command._info);
        }
    }
}
