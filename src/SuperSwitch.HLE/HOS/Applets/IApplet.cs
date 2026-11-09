using SuperSwitch.HLE.HOS.Services.Am.AppletAE;
using SuperSwitch.HLE.UI;
using SuperSwitch.Memory;
using System;
using System.Runtime.InteropServices;

namespace SuperSwitch.HLE.HOS.Applets
{
    interface IApplet
    {
        event EventHandler AppletStateChanged;

        ResultCode Start(AppletSession normalSession,
                         AppletSession interactiveSession);

        ResultCode GetResult() => ResultCode.Success;

        bool DrawTo(RenderingSurfaceInfo surfaceInfo, IVirtualMemoryManager destination, ulong position) => false;

        static T ReadStruct<T>(ReadOnlySpan<byte> data) where T : unmanaged => MemoryMarshal.Cast<byte, T>(data)[0];
    }
}
