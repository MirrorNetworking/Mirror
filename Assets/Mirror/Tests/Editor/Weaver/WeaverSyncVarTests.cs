using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncVarTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncVarsValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncVarsNoHook()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: No hook implementation found for System.Int32 MirrorTest.SyncVarsNoHook::health. Add this method to your class:\npublic void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { }"));
        }

        [Test]
        public void SyncVarsNoHookParams()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.SyncVarsNoHookParams::OnChangeHealth() should have signature:\npublic void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { }"));
        }

        [Test]
        public void SyncVarsTooManyHookParams()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.SyncVarsTooManyHookParams::OnChangeHealth(System.Int32,System.Int32,System.Int32) should have signature:\npublic void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { }"));
        }

        [Test]
        public void SyncVarsWrongHookType()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.SyncVarsWrongHookType::OnChangeHealth(System.Boolean,System.Boolean) should have signature:\npublic void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { }"));
        }

        [Test]
        public void SyncVarsDerivedNetworkBehaviour()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot generate writer for component type MirrorTest.SyncVarsDerivedNetworkBehaviour/MySyncVar. Use a supported type or provide a custom writer"));
        }

        [Test]
        public void SyncVarsStatic()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Int32 MirrorTest.SyncVarsStatic::invalidVar cannot be static"));
        }

        [Test]
        public void SyncVarsGenericParam()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot generate writer for generic type MirrorTest.SyncVarsGenericParam/MySyncVar`1<System.Int32>. Use a supported type or provide a custom writer"));
        }

        [Test]
        public void SyncVarsInterface()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot generate writer for interface MirrorTest.SyncVarsInterface/MySyncVar. Use a supported type or provide a custom writer"));
        }

        [Test]
        public void SyncVarsDifferentModule()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot generate writer for component type UnityEngine.TextMesh. Use a supported type or provide a custom writer"));
        }

        [Test]
        public void SyncVarsCantBeArray()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Int32[] MirrorTest.SyncVarsCantBeArray::thisShouldntWork has invalid type. Use SyncLists instead of arrays"));
        }

        [Test]
        public void SyncVarsSyncList()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
            Assert.That(weaverWarnings, Contains.Item("Mirror.Weaver warning: MirrorTest.SyncVarsSyncList/SyncObjImplementer MirrorTest.SyncVarsSyncList::syncobj has [SyncVar] attribute. SyncLists should not be marked with SyncVar"));
            Assert.That(weaverWarnings, Contains.Item("Mirror.Weaver warning: Mirror.SyncListInt MirrorTest.SyncVarsSyncList::syncints has [SyncVar] attribute. SyncLists should not be marked with SyncVar"));
        }

        [Test]
        public void SyncVarsMoreThan63()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.SyncVarsMoreThan63 has too many SyncVars. Consider refactoring your class into multiple components"));
        }
    }
}
