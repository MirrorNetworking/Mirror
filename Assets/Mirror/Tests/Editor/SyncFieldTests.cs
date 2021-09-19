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
        public void SetValue_SetsValue()
        {
            // .Value is a property which does several things.
            // make sure it .set actually sets the value
            field.Value = 1337;
            Assert.That(field.Value, Is.EqualTo(1337));
        }

        [Test]
        public void SetValue_CallsOnDirty()
        {
            // setting SyncField<T>.Value should call dirty
            field.Value = 1337;
            Assert.That(dirtyCalled, Is.EqualTo(1));
        }
    }
}
