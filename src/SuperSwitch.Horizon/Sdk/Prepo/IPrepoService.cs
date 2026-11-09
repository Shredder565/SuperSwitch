using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Account;
using SuperSwitch.Horizon.Sdk.Sf;
using System;
using ApplicationId = SuperSwitch.Horizon.Sdk.Ncm.ApplicationId;

namespace SuperSwitch.Horizon.Sdk.Prepo
{
    interface IPrepoService : IServiceObject
    {
        Result SaveReportOld(ReadOnlySpan<byte> gameRoomBuffer, ReadOnlySpan<byte> reportBuffer, ulong pid);
        Result SaveReport(ReadOnlySpan<byte> gameRoomBuffer, ReadOnlySpan<byte> reportBuffer, ulong pid, bool optInCheckEnabled);
        Result SaveReportWithUserOld(Uid userId, ReadOnlySpan<byte> gameRoomBuffer, ReadOnlySpan<byte> reportBuffer, ulong pid);
        Result SaveReportWithUser(Uid userId, ReadOnlySpan<byte> gameRoomBuffer, ReadOnlySpan<byte> reportBuffer, ulong pid, bool optInCheckEnabled);
        Result RequestImmediateTransmission();
        Result GetTransmissionStatus(out int status);
        Result GetSystemSessionId(out ulong systemSessionId);
        Result SaveSystemReport(ReadOnlySpan<byte> gameRoomBuffer, ApplicationId applicationId, ReadOnlySpan<byte> reportBuffer);
        Result SaveSystemReportWithUser(Uid userId, ReadOnlySpan<byte> gameRoomBuffer, ApplicationId applicationId, ReadOnlySpan<byte> reportBuffer);
        Result IsUserAgreementCheckEnabled(out bool enabled);
        Result SetUserAgreementCheckEnabled(bool enabled);
    }
}
