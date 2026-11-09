using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Server.Mix;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Server
{
    class MixInfoTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x940, Unsafe.SizeOf<MixInfo>());
        }
    }
}
