using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class SyncVarTests : MirrorTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // SyncVar<T> hooks are only called while client is active for now.
            // so we need an active client.
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        // SyncField<GameObject> should recommend SyncFielGameObject instead
        [Test]
        public void SyncFieldGameObject_Recommendation()
        {
            // should show even if value is null since T is <GameObject>
            LogAssert.Expect(LogType.Warning, new Regex($"Use explicit {nameof(SyncVarGameObject)}.*"));
            SyncVar<GameObject> _ = new SyncVar<GameObject>(null);
        }

        // SyncField<NetworkIdentity> should recommend SyncFielNetworkIdentity instead
        [Test]
        public void SyncFieldNetworkIdentity_Recommendation()
        {
            // should show even if value is null since T is <NetworkIdentity>
            LogAssert.Expect(LogType.Warning, new Regex($"Use explicit {nameof(SyncVarNetworkIdentity)}.*"));
            SyncVar<NetworkIdentity> _ = new SyncVar<NetworkIdentity>(null);
        }

        // SyncField<NetworkBehaviour> should recommend SyncFielNetworkBehaviour instead
        [Test]
        public void SyncFieldNetworkBehaviour_Recommendation()
        {
            // should show even if value is null since T is <NetworkBehaviour>
            LogAssert.Expect(LogType.Warning, new Regex($"Use explicit SyncVarNetworkBehaviour.*"));
            SyncVar<NetworkBehaviour> _ = new SyncVar<NetworkBehaviour>(null);
        }

        [Test]
        public void SetValue_SetsValue()
        {
            // .Value is a property which does several things.
            // make sure it .set actually sets the value
            SyncVar<int> field = 42;

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.Value = 1337;
            Assert.That(field.Value, Is.EqualTo(1337));
        }

        [Test]
        public void SetValue_CallsOnDirty()
        {
            SyncVar<int> field = 42;
            int dirtyCalled = 0;
            field.OnDirty = () => ++dirtyCalled;

            // setting SyncField<T>.Value should call dirty
            field.Value = 1337;
            Assert.That(dirtyCalled, Is.EqualTo(1));
        }

        [Test]
        public void SetValue_CallsOnDirty_OnlyIfValueChanged()
        {
            SyncVar<int> field = 42;
            int dirtyCalled = 0;
            field.OnDirty = () => ++dirtyCalled;

            // setting same value should not call OnDirty again
            field.Value = 42;
            Assert.That(dirtyCalled, Is.EqualTo(0));
        }

        [Test]
        public void ImplicitTo()
        {
            SyncVar<int> field = new SyncVar<int>(42);
            // T = field implicit conversion should get .Value
            int value = field;
            Assert.That(value, Is.EqualTo(42));
        }

        [Test]
        public void ImplicitFrom_SetsValue()
        {
            // field = T implicit conversion should set .Value
            SyncVar<int> field = 42;
            Assert.That(field.Value, Is.EqualTo(42));
        }

        [Test]
        public void Hook_IsCalled()
        {
            int called = 0;
            void OnChanged(int oldValue, int newValue)
            {
                ++called;
                Assert.That(oldValue, Is.EqualTo(42));
                Assert.That(newValue, Is.EqualTo(1337));
            }

            SyncVar<int> field = 42;
            field.Callback += OnChanged;

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            field.Value = 1337;
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void Hook_OnlyCalledIfValueChanged()
        {
            int called = 0;
            void OnChanged(int oldValue, int newValue)
            {
                ++called;
                Assert.That(oldValue, Is.EqualTo(42));
                Assert.That(newValue, Is.EqualTo(1337));
            }

            SyncVar<int> field = 42;
            field.Callback += OnChanged;
            // assign same value again. hook shouldn't be called again.
            field.Value = 42;
            Assert.That(called, Is.EqualTo(0));
        }

        [Test]
        public void Hook_Set_DoesntDeadlock()
        {
            // Value.set calls the hook.
            // calling Value.set inside the hook would deadlock.
            // this needs to be prevented.
            SyncVar<int> field = null;
            int called = 0;
            void OnChanged(int oldValue, int newValue)
            {
                // setting a different value calls setter -> hook again
                field.Value = 0;
                ++called;
            }
            field = 42;
            field.Callback += OnChanged;

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

            // setting a different value will call the hook
            field.Value = 1337;
            // in the end, hook should've been called exactly once
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
            SyncVar<int> field = 42;
            field.Callback += OnChanged;

            // avoid 'not initialized' exception
            field.OnDirty = () => {};

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
            SyncVar<int> fieldWithHook = 42;
            fieldWithHook.Callback += OnChanged;

            // avoid 'not initialized' exception
            fieldWithHook.OnDirty = () => {};

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
            SyncVar<int> field = 42;
            Assert.That(field.Equals(42), Is.True);
        }

        [Test]
        public void EqualsNull()
        {
            // .Equals(null) should always be false. so that == null works.
            SyncVar<int> field = 42;
            Assert.That(field.Equals(null), Is.False);
        }

        [Test]
        public void EqualsEqualsT()
        {
            // == should compare .Value
            SyncVar<int> field = 42;
            Assert.That(field == 42, Is.True);
        }

        [Test]
        public void ToString_CallsValueToString()
        {
            SyncVar<int> field = 42;
            Assert.That(field.ToString(), Is.EqualTo("42"));
        }
    }
}
