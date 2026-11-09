namespace SuperSwitch.HLE.HOS.Services.Time.Clock
{
    class EphemeralNetworkSystemClockCore : SystemClockCore
    {
        public EphemeralNetworkSystemClockCore(SteadyClockCore steadyClockCore) : base(steadyClockCore) { }
    }
}
