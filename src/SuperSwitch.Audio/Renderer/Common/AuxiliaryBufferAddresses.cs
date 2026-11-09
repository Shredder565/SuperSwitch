using System.Runtime.InteropServices;

namespace SuperSwitch.Audio.Renderer.Common
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AuxiliaryBufferAddresses
    {
        public ulong SendBufferInfo;
        public ulong SendBufferInfoBase;
        public ulong ReturnBufferInfo;
        public ulong ReturnBufferInfoBase;
    }
}
