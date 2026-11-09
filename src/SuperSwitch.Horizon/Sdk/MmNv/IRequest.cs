using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Sdk.MmNv
{
    interface IRequest : IServiceObject
    {
        Result InitializeOld(Module module, uint fgmPriority, uint autoClearEvent);
        Result FinalizeOld(Module module);
        Result SetAndWaitOld(Module module, uint clockRateMin, uint timeout);
        Result GetOld(out uint clockRateActual, Module module);
        Result Initialize(out uint requestId, Module module, uint fgmPriority, uint autoClearEvent);
        Result Finalize(uint requestId);
        Result SetAndWait(uint requestId, uint clockRateMin, uint timeout);
        Result Get(out uint clockRateActual, uint requestId);
    }
}
