using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Applet;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Sdk.Audio.Detail
{
    interface IAudioRendererManager : IServiceObject
    {
        Result OpenAudioRenderer(
            out IAudioRenderer renderer,
            AudioRendererParameterInternal parameter,
            int processHandle,
            int workBufferHandle,
            ulong workBufferSize,
            AppletResourceUserId appletUserId,
            ulong pid);
        Result GetWorkBufferSize(out long workBufferSize, AudioRendererParameterInternal parameter);
        Result GetAudioDeviceService(out IAudioDevice audioDevice, AppletResourceUserId appletUserId);
        Result OpenAudioRendererForManualExecution(
            out IAudioRenderer renderer,
            AudioRendererParameterInternal parameter,
            ulong workBufferAddress,
            int processHandle,
            ulong workBufferSize,
            AppletResourceUserId appletUserId,
            ulong pid);
        Result GetAudioDeviceServiceWithRevisionInfo(out IAudioDevice audioDevice, AppletResourceUserId appletUserId, uint revision);
    }
}
