using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer
{
    class VoiceInParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x170, Unsafe.SizeOf<VoiceInParameter1>());
            Assert.AreEqual(0x188, Unsafe.SizeOf<VoiceInParameter2>());
        }
    }
}
