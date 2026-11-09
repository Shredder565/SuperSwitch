using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Arp;
using SuperSwitch.Horizon.Sdk.Arp.Detail;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Arp.Ipc
{
    partial class UnregistrationNotifier : IUnregistrationNotifier, IServiceObject
    {
        private readonly ApplicationInstanceManager _applicationInstanceManager;

        public UnregistrationNotifier(ApplicationInstanceManager applicationInstanceManager)
        {
            _applicationInstanceManager = applicationInstanceManager;
        }

        [CmifCommand(0)]
        public Result GetReadableHandle([CopyHandle] out int readableHandle)
        {
            readableHandle = _applicationInstanceManager.EventHandle;

            return Result.Success;
        }
    }
}
