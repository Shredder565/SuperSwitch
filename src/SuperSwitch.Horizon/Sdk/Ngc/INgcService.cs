using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Sf;
using System;

namespace SuperSwitch.Horizon.Sdk.Ngc
{
    interface INgcService : IServiceObject
    {
        Result GetContentVersion(out uint version);
        Result Check(out uint checkMask, ReadOnlySpan<byte> text, uint regionMask, ProfanityFilterOption option);
        Result Mask(out int maskedWordsCount, Span<byte> filteredText, ReadOnlySpan<byte> text, uint regionMask, ProfanityFilterOption option);
        Result Reload();
    }
}
