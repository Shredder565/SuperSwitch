using System.Collections.Generic;

namespace SuperSwitch.Horizon.Sdk.Sf
{
    interface IServiceObject
    {
        IReadOnlyDictionary<int, CommandHandler> GetCommandHandlers();
    }
}
