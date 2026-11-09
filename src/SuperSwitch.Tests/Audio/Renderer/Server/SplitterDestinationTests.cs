using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Server.Splitter;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Server
{
    class SplitterDestinationTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0xE0, Unsafe.SizeOf<SplitterDestinationVersion1>());
            Assert.AreEqual(0x128, Unsafe.SizeOf<SplitterDestinationVersion2>());
        }
    }
}
