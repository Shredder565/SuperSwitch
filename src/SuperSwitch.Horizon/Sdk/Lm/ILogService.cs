using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.LogManager.Ipc;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Sdk.Lm
{
    interface ILogService : IServiceObject
    {
        Result OpenLogger(out LmLogger logger, ulong pid);
    }
}
