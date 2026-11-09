using System.Runtime.InteropServices;

namespace SuperSwitch.HLE.HOS.Services.Nv.NvDrvServices.NvMap
{
    [StructLayout(LayoutKind.Sequential)]
    struct NvMapCreate
    {
        public uint Size;
        public int Handle;
    }
}
