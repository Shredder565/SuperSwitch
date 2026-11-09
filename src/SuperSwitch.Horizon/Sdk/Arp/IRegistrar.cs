using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Ns;

namespace SuperSwitch.Horizon.Sdk.Arp
{
    public interface IRegistrar
    {
        public Result Issue(out ulong applicationInstanceId);
        public Result SetApplicationLaunchProperty(ApplicationLaunchProperty applicationLaunchProperty);
        public Result SetApplicationControlProperty(in ApplicationControlProperty applicationControlProperty);
    }
}
