using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Ncm;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Sdk.Bcat
{
    internal interface IServiceCreator : IServiceObject
    {
        Result CreateBcatService(out IBcatService service, ulong pid);
        Result CreateDeliveryCacheStorageService(out IDeliveryCacheStorageService service, ulong pid);
        Result CreateDeliveryCacheStorageServiceWithApplicationId(out IDeliveryCacheStorageService service, ApplicationId applicationId);
    }
}
