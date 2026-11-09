using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Sdk.Bcat
{
    internal interface IBcatService : IServiceObject
    {
        Result RequestSyncDeliveryCache(out IDeliveryCacheProgressService deliveryCacheProgressService);
    }
}
