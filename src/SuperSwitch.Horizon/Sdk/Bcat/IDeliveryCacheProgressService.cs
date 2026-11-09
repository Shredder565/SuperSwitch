using SuperSwitch.Horizon.Bcat.Ipc.Types;
using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Sdk.Bcat
{
    internal interface IDeliveryCacheProgressService : IServiceObject
    {
        Result GetEvent(out int handle);
        Result GetImpl(out DeliveryCacheProgressImpl deliveryCacheProgressImpl);
    }
}
