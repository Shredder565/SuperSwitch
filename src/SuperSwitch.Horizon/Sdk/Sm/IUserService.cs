using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Sdk.Sm
{
    interface IUserService : IServiceObject
    {
        Result Initialize(ulong clientProcessId);
        Result GetService(out int handle, ServiceName name);
        Result RegisterService(out int handle, ServiceName name, int maxSessions, bool isLight);
        Result UnregisterService(ServiceName name);
    }
}
