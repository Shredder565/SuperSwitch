namespace SuperSwitch.Common.Recording
{
    public enum RecordingCodec
    {
        H264,
        Hevc,
    }

    public enum RecordingQuality
    {
        High,
        Balanced,
        Small,
        /// <summary>
        /// High quality at a much smaller size: slower encoder preset with adaptive quantization and lookahead.
        /// </summary>
        Efficient,
    }

    /// <summary>
    /// SuperSwitch recorder options, set from the emulator configuration.
    /// </summary>
    public static class RecordingSettings
    {
        public static RecordingCodec Codec { get; set; } = RecordingCodec.H264;

        public static RecordingQuality Quality { get; set; } = RecordingQuality.High;

        public static bool ReplayBufferEnabled { get; set; }

        public static int ReplayBufferSeconds { get; set; } = 30;

        /// <summary>
        /// Start recording automatically whenever a game starts.
        /// </summary>
        public static bool AutoRecord { get; set; }
    }
}
