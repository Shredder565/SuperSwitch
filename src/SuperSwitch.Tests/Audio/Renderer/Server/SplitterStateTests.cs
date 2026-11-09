using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Server.Splitter;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Server
{
    class SplitterStateTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x20, Unsafe.SizeOf<SplitterState>());
        }
    }
}
