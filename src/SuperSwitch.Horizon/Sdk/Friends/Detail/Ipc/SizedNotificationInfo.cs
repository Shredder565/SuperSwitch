using SuperSwitch.Horizon.Sdk.Account;
using System.Runtime.InteropServices;

namespace SuperSwitch.Horizon.Sdk.Friends.Detail.Ipc
{
    [StructLayout(LayoutKind.Sequential, Size = 0x10, Pack = 0x8)]
    struct SizedNotificationInfo
    {
        public NotificationEventType Type;
        public uint Padding;
        public NetworkServiceAccountId NetworkUserIdPlaceholder;
    }
}
