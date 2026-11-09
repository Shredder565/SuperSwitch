using System.Runtime.InteropServices;

namespace SuperSwitch.HLE.HOS.Services.Nv.NvDrvServices.NvHostCtrlGpu.Types
{
    [StructLayout(LayoutKind.Sequential)]
    struct ZcullGetCtxSizeArguments
    {
        public int Size;
    }
}
