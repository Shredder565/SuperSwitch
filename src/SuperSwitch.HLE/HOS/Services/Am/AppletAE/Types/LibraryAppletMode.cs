using System;

namespace SuperSwitch.HLE.HOS.Services.Am.AppletAE
{
    [Flags]
    enum LibraryAppletMode : uint
    {
        AllForeground,
        PartialForeground,
        NoUi,
        PartialForegroundWithIndirectDisplay,
        AllForegroundInitiallyHidden,
    }
}
