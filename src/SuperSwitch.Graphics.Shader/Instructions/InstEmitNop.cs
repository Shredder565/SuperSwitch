using SuperSwitch.Graphics.Shader.Decoders;
using SuperSwitch.Graphics.Shader.Translation;

namespace SuperSwitch.Graphics.Shader.Instructions
{
    static partial class InstEmit
    {
        public static void Nop(EmitterContext context)
        {
            context.GetOp<InstNop>();

            // No operation.
        }
    }
}
