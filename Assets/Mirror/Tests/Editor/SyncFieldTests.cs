using NUnit.Framework;

namespace Mirror.Tests
{
    public class SyncFieldTests
    {
        [Test]
        public void SetValue_SetsValue()
        {
            // .Value is a property which does several things.
            // make sure it .set actually sets the value
            SyncField<int> field = 42;
            field.Value = 1337;
            Assert.That(field.Value, Is.EqualTo(1337));
        }

        [Test]
        public void SetValue_CallsOnDirty()
        {
            SyncField<int> field = 42;
            int dirtyCalled = 0;
            field.OnDirty = () => ++dirtyCalled;

            // setting SyncField<T>.Value should call dirty
            field.Value = 1337;
            Assert.That(dirtyCalled, Is.EqualTo(1));
        }

        [Test]
        public void SetValue_WithoutOnDirty()
        {
            // OnDirty needs to be optional.
            // shouldn't throw exceptions if OnDirty is null.
            SyncField<int> field = 42;
            field.Value = 1337;
        }

        [Test]
        public void ImplicitTo()
        {
            // T = field implicit conversion should get .Value
            SyncField<int> field = 42;
            int value = field;
            Assert.That(value, Is.EqualTo(42));
        }

        // TODO maybe implicit from shouldn't exist?
        //      should be readonly.
        //      or a struct and copy OnDirty hook
        [Test]
        public void ImplicitFrom_SetsValue()
        {
            // field = T implicit conversion should set .Value
            SyncField<int> field = 42;
            field = 1337;
            Assert.That(field.Value, Is.EqualTo(1337));
        }

        // TODO maybe implicit from shouldn't exist?
        //      should be readonly.
        //      or a struct and copy OnDirty hook
        [Test]
        public void ImplicitFrom_CallsOnDirty()
        {
            SyncField<int> field = 42;
            int dirtyCalled = 0;
            field.OnDirty = () => ++dirtyCalled;

            // field = T implicit conversion should call OnDirty
            field = 1337;
            Assert.That(dirtyCalled, Is.EqualTo(1));
        }

        [Test, Ignore("TODO: what should copy ctor do?")]
        public void CopyConstructor()
        {
            SyncField<int> field = 42;
            SyncField<int> other = new SyncField<int>(43);
            field = new SyncField<int>(other);
        }

        [Test]
        public void Hook()
        {
            int called = 0;
            void OnChanged(int oldValue, int newValue)
            {
                ++called;
                Assert.That(oldValue, Is.EqualTo(42));
                Assert.That(newValue, Is.EqualTo(1337));
            }

            SyncField<int> field = new SyncField<int>(42, OnChanged);
            field.Value = 1337;
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void DeserializeAll_CallsHook()
        {
            // create field with hook
            int called = 0;
            void OnChanged(int oldValue, int newValue)
            {
                ++called;
                Assert.That(oldValue, Is.EqualTo(42));
                Assert.That(newValue, Is.EqualTo(1337));
            }
            SyncField<int> field = new SyncField<int>(42, OnChanged);

            // create reader with data
            NetworkWriter writer = new NetworkWriter();
            writer.WriteInt(1337);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());

            // deserialize
            field.OnDeserializeAll(reader);
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void DeserializeDelta_CallsHook()
        {
            // create field with hook
            int called = 0;
            void OnChanged(int oldValue, int newValue)
            {
                ++called;
                Assert.That(oldValue, Is.EqualTo(42));
                Assert.That(newValue, Is.EqualTo(1337));
            }
            SyncField<int> fieldWithHook = new SyncField<int>(42, OnChanged);

            // create reader with data
            NetworkWriter writer = new NetworkWriter();
            writer.WriteInt(1337);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());

            // deserialize
            fieldWithHook.OnDeserializeDelta(reader);
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void EqualsT()
        {
            // .Equals should compare .Value
            SyncField<int> field = 42;
            Assert.That(field.Equals(42), Is.True);
        }

        [Test]
        public void EqualsNull()
        {
            // .Equals(null) should always be false. so that == null works.
            SyncField<int> field = 42;
            Assert.That(field.Equals(null), Is.False);
        }

        [Test]
        public void EqualsEqualsT()
        {
            // == should compare .Value
            SyncField<int> field = 42;
            Assert.That(field == 42, Is.True);
        }

        [Test]
        public void ToString_CallsValueToString()
        {
            SyncField<int> field = 42;
            Assert.That(field.ToString(), Is.EqualTo("42"));
        }
    }
}
