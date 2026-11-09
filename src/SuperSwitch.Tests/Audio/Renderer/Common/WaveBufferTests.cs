using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Common;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Common
{
    class WaveBufferTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x30, Unsafe.SizeOf<WaveBuffer>());
        }
    }
}
