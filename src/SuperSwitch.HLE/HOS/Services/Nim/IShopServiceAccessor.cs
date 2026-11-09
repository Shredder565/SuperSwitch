using SuperSwitch.Common.Logging;
using SuperSwitch.HLE.HOS.Ipc;
using SuperSwitch.HLE.HOS.Kernel.Threading;
using SuperSwitch.HLE.HOS.Services.Nim.ShopServiceAccessServerInterface.ShopServiceAccessServer.ShopServiceAccessor;
using SuperSwitch.Horizon.Common;
using System;

namespace SuperSwitch.HLE.HOS.Services.Nim.ShopServiceAccessServerInterface.ShopServiceAccessServer
{
    class IShopServiceAccessor : IpcService
    {
        private readonly KEvent _event;

        private int _eventHandle;

        public IShopServiceAccessor(Horizon system)
        {
            _event = new KEvent(system.KernelContext);
        }

        [CommandCmif(0)]
        // CreateAsyncInterface(u64) -> (handle<copy>, object<nn::ec::IShopServiceAsync>)
        public ResultCode CreateAsyncInterface(ServiceCtx context)
        {
            MakeObject(context, new IShopServiceAsync());

            if (_eventHandle == 0)
            {
                if (context.Process.HandleTable.GenerateHandle(_event.ReadableEvent, out _eventHandle) != Result.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_eventHandle);

            Logger.Stub?.PrintStub(LogClass.ServiceNim);

            return ResultCode.Success;
        }
    }
}
