using SuperSwitch.Common.Memory;

namespace SuperSwitch.Graphics.Nvdec.Vp9.Types
{
    internal struct MvRef
    {
        public Array2<Mv> Mv;
        public Array2<sbyte> RefFrame;
    }
}
