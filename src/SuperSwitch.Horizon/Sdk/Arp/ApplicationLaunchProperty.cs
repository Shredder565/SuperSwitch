using SuperSwitch.Horizon.Sdk.Ncm;

namespace SuperSwitch.Horizon.Sdk.Arp
{
    public struct ApplicationLaunchProperty
    {
        public ApplicationId ApplicationId;
        public uint Version;
        public StorageId Storage;
        public StorageId PatchStorage;
        public ApplicationKind ApplicationKind;
        public byte Padding;
    }
}
