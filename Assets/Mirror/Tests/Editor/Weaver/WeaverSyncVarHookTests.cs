using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncVarHookTests: WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncVarsNoHook()
        {
            Assert.That(weaverErrors, Contains.Item("No hook implementation found for health. Add this method to your class: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Int32 WeaverSyncVarTests.SyncVarsNoHook.SyncVarsNoHook::health)"));
        }

        [Test]
        public void SyncVarsNoHookParams()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void WeaverSyncVarTests.SyncVarsNoHookParams.SyncVarsNoHookParams::OnChangeHealth())"));
        }

        [Test]
        public void SyncVarsTooManyHookParams()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void WeaverSyncVarTests.SyncVarsTooManyHookParams.SyncVarsTooManyHookParams::OnChangeHealth(System.Int32,System.Int32,System.Int32))"));
        }

        [Test]
        public void SyncVarsWrongHookType()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void WeaverSyncVarTests.SyncVarsWrongHookType.SyncVarsWrongHookType::OnChangeHealth(System.Boolean,System.Boolean))"));
        }
    }
}
