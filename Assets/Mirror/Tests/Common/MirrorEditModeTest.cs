// base class for networking tests to make things easier.
using NUnit.Framework;

namespace Mirror.Tests
{
    public abstract class MirrorEditModeTest : MirrorTest
    {
        [SetUp]
        public override void SetUp() => base.SetUp();

        [TearDown]
        public override void TearDown() => base.TearDown();
    }
}
