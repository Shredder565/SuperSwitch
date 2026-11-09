using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer
{
    class VoiceOutStatusTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x10, Unsafe.SizeOf<VoiceOutStatus>());
        }
    }
}
