using SuperSwitch.Horizon.Common;

namespace SuperSwitch.Horizon.Sdk.Arp
{
    public interface IUnregistrationNotifier
    {
        public Result GetReadableHandle(out int readableHandle);
    }
}
