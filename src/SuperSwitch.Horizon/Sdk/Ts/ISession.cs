using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Sdk.Ts
{
    interface ISession : IServiceObject
    {
        Result GetTemperatureRange(out int minimumTemperature, out int maximumTemperature);
        Result GetTemperature(out int temperature);
        Result SetMeasurementMode(byte measurementMode);
    }
}
