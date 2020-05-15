using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncVarHookTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void AutoDetectsPrivateHook()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AutoDetectsNewHook()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AutoDetectsOldNewHook()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AutoDetectsOldNewInitialHook()
        {
            Assert.That(weaverErrors, Is.Empty);
        }


        [Test]
        public void FindsExplicitNewHookWithOtherOverloads()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void FindsExplicitOldNewHookWithOtherOverloads()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void FindsExplicitOldNewInitialHookWithOtherOverloads()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void ErrorWhenNoHookAutoDetected()
        {
            Assert.That(weaverErrors, Contains.Item("No hook implementation found for health. Add this method to your class: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Int32 WeaverSyncVarTests.SyncVarsNoHook.SyncVarsNoHook::health)"));
        }

        [Test]
        public void ErrorWhenNoHookWithCorrectParametersAutoDetected()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void WeaverSyncVarTests.SyncVarsNoHookParams.SyncVarsNoHookParams::OnChangeHealth())"));
        }

        [Test]
        public void ErrorWhenMultipleHooksAutoDetected()
        {
            Assert.That(weaverErrors, Contains.Item("No hook implementation found for health. Add this method to your class: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Int32 WeaverSyncVarTests.SyncVarsNoHook.SyncVarsNoHook::health)"));
        }

        [Test]
        public void ErrorWhenExplicitNewHookIsntFound()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void WeaverSyncVarTests.SyncVarsWrongHookType.SyncVarsWrongHookType::OnChangeHealth(System.Boolean,System.Boolean))"));
        }

        [Test]
        public void ErrorWhenExplicitOldNewHookIsntFound()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void WeaverSyncVarTests.SyncVarsWrongHookType.SyncVarsWrongHookType::OnChangeHealth(System.Boolean,System.Boolean))"));
        }

        [Test]
        public void ErrorWhenExplicitOldNewInitialHookIsntFound()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void WeaverSyncVarTests.SyncVarsWrongHookType.SyncVarsWrongHookType::OnChangeHealth(System.Boolean,System.Boolean))"));
        }

        [Test]
        public void ErrorWhenNewParametersInNewIsWrongType()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void WeaverSyncVarTests.SyncVarsWrongHookType.SyncVarsWrongHookType::OnChangeHealth(System.Boolean,System.Boolean))"));
        }

        [Test]
        public void ErrorWhenOldParametersInOldNewIsWrongType()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void WeaverSyncVarTests.SyncVarsWrongHookType.SyncVarsWrongHookType::OnChangeHealth(System.Boolean,System.Boolean))"));
        }

        [Test]
        public void ErrorWhenNewParametersInOldNewIsWrongType()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void WeaverSyncVarTests.SyncVarsWrongHookType.SyncVarsWrongHookType::OnChangeHealth(System.Boolean,System.Boolean))"));
        }

        [Test]
        public void ErrorWhenOldParametersInOldNewInitialIsWrongType()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void WeaverSyncVarTests.SyncVarsWrongHookType.SyncVarsWrongHookType::OnChangeHealth(System.Boolean,System.Boolean))"));
        }

        [Test]
        public void ErrorWhenNewParametersInOldNewInitialIsWrongType()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void WeaverSyncVarTests.SyncVarsWrongHookType.SyncVarsWrongHookType::OnChangeHealth(System.Boolean,System.Boolean))"));
        }

        [Test]
        public void ErrorWhenInitialParametersInOldNewInitialIsWrongType()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void WeaverSyncVarTests.SyncVarsWrongHookType.SyncVarsWrongHookType::OnChangeHealth(System.Boolean,System.Boolean))"));
        }
    }
}
