using SuperSwitch.Memory.Range;

namespace SuperSwitch.Graphics.Gpu.Memory
{
    /// <summary>
    /// GPU Vertex Buffer information.
    /// </summary>
    struct VertexBuffer
    {
        public MultiRange Range;
        public int Stride;
        public int Divisor;
    }
}
