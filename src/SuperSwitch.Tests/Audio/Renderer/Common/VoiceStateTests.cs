using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Common;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Common
{
    class VoiceStateTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.LessOrEqual(Unsafe.SizeOf<VoiceState>(), 0x100);
        }
    }
}
