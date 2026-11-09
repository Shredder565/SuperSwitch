namespace SuperSwitch.VideoConverter
{
    /// <summary>
    /// The same NVIDIA (NVENC) encoder settings SuperSwitch's recorder uses.
    /// </summary>
    internal static class EncodePresets
    {
        public static readonly string[] QualityNames =
        [
            "High quality, small files (recommended)",
            "High (visually lossless, large files)",
            "Balanced",
            "Small files",
        ];

        public static readonly string[] CodecNames =
        [
            "HEVC / H.265 (half the size, recommended)",
            "H.264 (plays everywhere)",
        ];

        /// <summary>
        /// FFmpeg video encoder arguments.
        /// </summary>
        /// <param name="quality">Index into <see cref="QualityNames"/></param>
        /// <param name="hevc">True for HEVC, false for H.264</param>
        public static string GetVideoArgs(int quality, bool hevc)
        {
            string codec = hevc
                ? "-c:v hevc_nvenc -profile:v main -tag:v hvc1 "
                : "-c:v h264_nvenc -profile:v high ";

            if (quality == 0)
            {
                // Visually high quality at a fraction of the size: slower preset, two-pass rate control at quarter
                // resolution, spatial and temporal adaptive quantization, lookahead and B-frames.
                string rate = hevc
                    ? "-cq 25 -maxrate 45M -bufsize 90M "
                    : "-cq 22 -maxrate 70M -bufsize 140M ";

                return codec + rate +
                       "-preset p6 -tune hq -rc vbr -b:v 0 -multipass qres -spatial-aq 1 -temporal-aq 1 -aq-strength 8 " +
                       "-rc-lookahead 20 -bf 3 -b_ref_mode middle ";
            }

            (int cq, int maxRateMbps) = quality switch
            {
                2 => (23, 80),
                3 => (28, 40),
                _ => (18, 160),
            };

            return codec + $"-preset p5 -tune hq -rc vbr -cq {cq} -b:v 0 -maxrate {maxRateMbps}M -bufsize {maxRateMbps * 2}M ";
        }
    }
}
