using NUnit.Framework;

namespace Mirror.Tests
{
    public class SyncFieldTests
    {
        SyncField<int> field;
        int dirtyCalled = 0;

        [SetUp]
        public void SetUp()
        {
            field = 42;
            dirtyCalled = 0;
            field.OnDirty = () => ++dirtyCalled;
        }

        [Test]
        public void SetValue_CallsOnDirty()
        {
            ++field.Value;
            Assert.That(dirtyCalled, Is.EqualTo(1));
        }
    }
}
