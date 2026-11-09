using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Sf;
using System;

namespace SuperSwitch.Horizon.Sdk.Lm
{
    interface ILmLogger : IServiceObject
    {
        Result Log(Span<byte> message);
        Result SetDestination(LogDestination destination);
    }
}
