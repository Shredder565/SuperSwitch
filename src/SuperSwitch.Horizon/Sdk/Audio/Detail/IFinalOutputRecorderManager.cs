using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Applet;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Sdk.Audio.Detail
{
    interface IFinalOutputRecorderManager : IServiceObject
    {
        Result OpenFinalOutputRecorder(
            out IFinalOutputRecorder recorder,
            FinalOutputRecorderParameter parameter,
            int processHandle,
            out FinalOutputRecorderParameterInternal outParameter,
            AppletResourceUserId appletResourceId);
    }
}
