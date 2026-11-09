using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Parameter
{
    class MixParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x930, Unsafe.SizeOf<MixParameter>());
        }
    }
}
