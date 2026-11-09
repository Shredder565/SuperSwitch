using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer
{
    class VoiceChannelResourceInParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x70, Unsafe.SizeOf<VoiceChannelResourceInParameter>());
        }
    }
}
