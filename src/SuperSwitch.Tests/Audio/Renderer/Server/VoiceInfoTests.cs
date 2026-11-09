using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Server.Voice;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Server
{
    class VoiceInfoTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.LessOrEqual(Unsafe.SizeOf<VoiceInfo>(), 0x238);
        }
    }
}
