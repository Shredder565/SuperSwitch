using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Parameter.Sink;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Parameter.Sink
{
    class CircularBufferParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x24, Unsafe.SizeOf<CircularBufferParameter>());
        }
    }
}
