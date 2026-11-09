using System.Diagnostics;

namespace SuperSwitch.Horizon.Sdk
{
    static class DebugUtil
    {
        public static void Assert(bool condition)
        {
            Debug.Assert(condition);
        }
    }
}
