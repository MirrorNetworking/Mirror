using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncVarTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncVarsValid()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncVarsNoHook()
        {
            Assert.That(weaverErrors, Contains.Item("No hook implementation found for health. Add this method to your class: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Int32 MirrorTest.SyncVarsNoHook::health)"));
        }

        [Test]
        public void SyncVarsNoHookParams()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void MirrorTest.SyncVarsNoHookParams::OnChangeHealth())"));
        }

        [Test]
        public void SyncVarsTooManyHookParams()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void MirrorTest.SyncVarsTooManyHookParams::OnChangeHealth(System.Int32,System.Int32,System.Int32))"));
        }

        [Test]
        public void SyncVarsWrongHookType()
        {
            Assert.That(weaverErrors, Contains.Item("OnChangeHealth should have signature: public void OnChangeHealth(System.Int32 oldValue, System.Int32 newValue) { } (at System.Void MirrorTest.SyncVarsWrongHookType::OnChangeHealth(System.Boolean,System.Boolean))"));
        }

        [Test]
        public void SyncVarsDerivedNetworkBehaviour()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type MySyncVar. Use a supported type or provide a custom writer (at MirrorTest.SyncVarsDerivedNetworkBehaviour/MySyncVar)"));
            Assert.That(weaverErrors, Contains.Item("invalidVar has unsupported type. Use a supported Mirror type instead (at MirrorTest.SyncVarsDerivedNetworkBehaviour/MySyncVar MirrorTest.SyncVarsDerivedNetworkBehaviour::invalidVar)"));
        }

        [Test]
        public void SyncVarsStatic()
        {
            Assert.That(weaverErrors, Contains.Item("invalidVar cannot be static (at System.Int32 MirrorTest.SyncVarsStatic::invalidVar)"));
        }

        [Test]
        public void SyncVarsGenericParam()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for generic type MySyncVar`1. Use a supported type or provide a custom writer (at MirrorTest.SyncVarsGenericParam/MySyncVar`1<System.Int32>)"));
            Assert.That(weaverErrors, Contains.Item("invalidVar has unsupported type. Use a supported Mirror type instead (at MirrorTest.SyncVarsGenericParam/MySyncVar`1<System.Int32> MirrorTest.SyncVarsGenericParam::invalidVar)"));
        }

        [Test]
        public void SyncVarsInterface()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for interface MySyncVar. Use a supported type or provide a custom writer (at MirrorTest.SyncVarsInterface/MySyncVar)"));
            Assert.That(weaverErrors, Contains.Item("invalidVar has unsupported type. Use a supported Mirror type instead (at MirrorTest.SyncVarsInterface/MySyncVar MirrorTest.SyncVarsInterface::invalidVar)"));
        }

        [Test]
        public void SyncVarsDifferentModule()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type TextMesh. Use a supported type or provide a custom writer (at UnityEngine.TextMesh)"));
            Assert.That(weaverErrors, Contains.Item("invalidVar has unsupported type. Use a supported Mirror type instead (at UnityEngine.TextMesh MirrorTest.SyncVarsDifferentModule::invalidVar)"));
        }

        [Test]
        public void SyncVarsCantBeArray()
        {
            Assert.That(weaverErrors, Contains.Item("thisShouldntWork has invalid type. Use SyncLists instead of arrays (at System.Int32[] MirrorTest.SyncVarsCantBeArray::thisShouldntWork)"));
        }

        [Test]
        public void SyncVarsSyncList()
        {
            Assert.That(weaverErrors, Is.Empty);
            Assert.That(weaverWarnings, Contains.Item("syncobj has [SyncVar] attribute. SyncLists should not be marked with SyncVar (at MirrorTest.SyncVarsSyncList/SyncObjImplementer MirrorTest.SyncVarsSyncList::syncobj)"));
            Assert.That(weaverWarnings, Contains.Item("syncints has [SyncVar] attribute. SyncLists should not be marked with SyncVar (at Mirror.SyncListInt MirrorTest.SyncVarsSyncList::syncints)"));
        }

        [Test]
        public void SyncVarsMoreThan63()
        {
            Assert.That(weaverErrors, Contains.Item("SyncVarsMoreThan63 has too many SyncVars. Consider refactoring your class into multiple components (at MirrorTest.SyncVarsMoreThan63)"));
        }
    }
}
