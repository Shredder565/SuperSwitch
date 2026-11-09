using SuperSwitch.Graphics.GAL;
using SuperSwitch.Memory.Range;

namespace SuperSwitch.Graphics.Gpu.Memory
{
    /// <summary>
    /// GPU Index Buffer information.
    /// </summary>
    struct IndexBuffer
    {
        public MultiRange Range;
        public IndexType Type;
    }
}
