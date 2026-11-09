using SuperSwitch.Horizon.Bcat.Types;
using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Bcat;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Bcat.Ipc
{
    partial class BcatService : IBcatService
    {
        public BcatService(BcatServicePermissionLevel permissionLevel) { }

        [CmifCommand(10100)]
        public Result RequestSyncDeliveryCache(out IDeliveryCacheProgressService deliveryCacheProgressService)
        {
            deliveryCacheProgressService = new DeliveryCacheProgressService();

            return Result.Success;
        }
    }
}
