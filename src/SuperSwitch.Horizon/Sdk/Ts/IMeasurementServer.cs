using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Sf;

namespace SuperSwitch.Horizon.Sdk.Ts
{
    interface IMeasurementServer : IServiceObject
    {
        Result GetTemperatureRange(out int minimumTemperature, out int maximumTemperature, Location location);
        Result GetTemperature(out int temperature, Location location);
        Result SetMeasurementMode(Location location, byte measurementMode);
        Result GetTemperatureMilliC(out int temperatureMilliC, Location location);
        Result OpenSession(out ISession session, DeviceCode deviceCode);
    }
}
