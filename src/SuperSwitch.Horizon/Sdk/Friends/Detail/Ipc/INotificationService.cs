using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Sdk.Friends.Detail.Ipc
{
    interface INotificationService : IServiceObject
    {
        Result GetEvent(out int eventHandle);
        Result Clear();
        Result Pop(out SizedNotificationInfo sizedNotificationInfo);
    }
}
