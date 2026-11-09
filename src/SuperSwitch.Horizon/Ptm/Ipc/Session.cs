using SuperSwitch.Common.Logging;
using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Ptm.Ipc;
using SuperSwitch.Horizon.Sdk.Sf;
using SuperSwitch.Horizon.Sdk.Ts;

namespace SuperSwitch.Horizon.Ts.Ipc
{
    partial class Session : ISession
    {
        private readonly DeviceCode _deviceCode;

        public Session(DeviceCode deviceCode)
        {
            _deviceCode = deviceCode;
        }

        [CmifCommand(0)]
        public Result GetTemperatureRange(out int minimumTemperature, out int maximumTemperature)
        {
            Logger.Stub?.PrintStub(LogClass.ServicePtm, new { _deviceCode });

            minimumTemperature = MeasurementServer.MinimumTemperature;
            maximumTemperature = MeasurementServer.MaximumTemperature;

            return Result.Success;
        }

        [CmifCommand(2)]
        public Result SetMeasurementMode(byte measurementMode)
        {
            Logger.Stub?.PrintStub(LogClass.ServicePtm, new { _deviceCode, measurementMode });

            return Result.Success;
        }

        [CmifCommand(4)]
        public Result GetTemperature(out int temperature)
        {
            Logger.Stub?.PrintStub(LogClass.ServicePtm, new { _deviceCode });

            temperature = MeasurementServer.DefaultTemperature;

            return Result.Success;
        }
    }
}
