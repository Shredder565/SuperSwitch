using SuperSwitch.Common.Logging;
using SuperSwitch.HLE.HOS.Ipc;
using SuperSwitch.HLE.HOS.Kernel.Threading;
using SuperSwitch.Horizon.Common;
using System;

namespace SuperSwitch.HLE.HOS.Services.Notification
{
    class INotificationSystemEventAccessor : IpcService
    {

        private readonly KEvent _getNotificationSendingNotifierEvent;
        private int _getNotificationSendingNotifierEventHandle;

        public INotificationSystemEventAccessor(ServiceCtx context)
        {
            _getNotificationSendingNotifierEvent = new KEvent(context.Device.System.KernelContext);
        }

        [CommandCmif(0)] // 9.0.0+
        // GetNotificationSendingNotifier() -> nn::notification::server::INotificationSystemEventAccessor
        public ResultCode GetSystemEvent(ServiceCtx context)
        {
            if (_getNotificationSendingNotifierEventHandle == 0)
            {
                if (context.Process.HandleTable.GenerateHandle(_getNotificationSendingNotifierEvent.ReadableEvent, out _getNotificationSendingNotifierEventHandle) != Result.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_getNotificationSendingNotifierEventHandle);
            return ResultCode.Success;
        }
    }
}
