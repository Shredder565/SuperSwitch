using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer
{
    class AudioRendererConfigurationTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x34, Unsafe.SizeOf<AudioRendererConfiguration>());
        }
    }
}
