using System;

namespace SuperSwitch.Horizon.Sdk.Settings.System
{
    [Flags]
    enum ServiceDiscoveryControlSettings : uint
    {
        IsChangeEnvironmentIdentifierDisabled = 1 << 0,
    }
}
