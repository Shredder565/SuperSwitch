using SuperSwitch.Common.Memory;

namespace SuperSwitch.Graphics.Nvdec.Vp9.Types
{
    internal struct LoopFilterInfoN
    {
        public Array64<LoopFilterThresh> Lfthr;
        public Array8<Array4<Array2<byte>>> Lvl;
    }
}
