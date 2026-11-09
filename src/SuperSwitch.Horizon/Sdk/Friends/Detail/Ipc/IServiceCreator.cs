using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Account;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Sdk.Friends.Detail.Ipc
{
    interface IServiceCreator : IServiceObject
    {
        Result CreateFriendService(out IFriendService friendService);
        Result CreateNotificationService(out INotificationService notificationService, Uid userId);
        Result CreateDaemonSuspendSessionService(out IDaemonSuspendSessionService daemonSuspendSessionService);
    }
}
