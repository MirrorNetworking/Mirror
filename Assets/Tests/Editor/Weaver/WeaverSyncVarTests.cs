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
        public void SyncVarArraySegment()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncVarsDerivedNetworkBehaviour()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type MySyncVar. Use a supported type or provide a custom writer (at WeaverSyncVarTests.SyncVarsDerivedNetworkBehaviour.SyncVarsDerivedNetworkBehaviour/MySyncVar)"));
            Assert.That(weaverErrors, Contains.Item("invalidVar has unsupported type. Use a supported Mirror type instead (at WeaverSyncVarTests.SyncVarsDerivedNetworkBehaviour.SyncVarsDerivedNetworkBehaviour/MySyncVar WeaverSyncVarTests.SyncVarsDerivedNetworkBehaviour.SyncVarsDerivedNetworkBehaviour::invalidVar)"));
        }

        [Test]
        public void SyncVarsStatic()
        {
            Assert.That(weaverErrors, Contains.Item("invalidVar cannot be static (at System.Int32 WeaverSyncVarTests.SyncVarsStatic.SyncVarsStatic::invalidVar)"));
        }

        [Test]
        public void SyncVarsGenericParam()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for generic type MySyncVar`1. Use a supported type or provide a custom writer (at WeaverSyncVarTests.SyncVarsGenericParam.SyncVarsGenericParam/MySyncVar`1<System.Int32>)"));
            Assert.That(weaverErrors, Contains.Item("invalidVar has unsupported type. Use a supported Mirror type instead (at WeaverSyncVarTests.SyncVarsGenericParam.SyncVarsGenericParam/MySyncVar`1<System.Int32> WeaverSyncVarTests.SyncVarsGenericParam.SyncVarsGenericParam::invalidVar)"));
        }

        [Test]
        public void SyncVarsInterface()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for interface MySyncVar. Use a supported type or provide a custom writer (at WeaverSyncVarTests.SyncVarsInterface.SyncVarsInterface/MySyncVar)"));
            Assert.That(weaverErrors, Contains.Item("invalidVar has unsupported type. Use a supported Mirror type instead (at WeaverSyncVarTests.SyncVarsInterface.SyncVarsInterface/MySyncVar WeaverSyncVarTests.SyncVarsInterface.SyncVarsInterface::invalidVar)"));
        }

        [Test]
        public void SyncVarsDifferentModule()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type TextMesh. Use a supported type or provide a custom writer (at UnityEngine.TextMesh)"));
            Assert.That(weaverErrors, Contains.Item("invalidVar has unsupported type. Use a supported Mirror type instead (at UnityEngine.TextMesh WeaverSyncVarTests.SyncVarsDifferentModule.SyncVarsDifferentModule::invalidVar)"));
        }

        [Test]
        public void SyncVarsCantBeArray()
        {
            Assert.That(weaverErrors, Contains.Item("thisShouldntWork has invalid type. Use SyncLists instead of arrays (at System.Int32[] WeaverSyncVarTests.SyncVarsCantBeArray.SyncVarsCantBeArray::thisShouldntWork)"));
        }

        [Test]
        public void SyncVarsSyncList()
        {
            Assert.That(weaverErrors, Is.Empty);
            Assert.That(weaverWarnings, Contains.Item("syncobj has [SyncVar] attribute. SyncLists should not be marked with SyncVar (at WeaverSyncVarTests.SyncVarsSyncList.SyncVarsSyncList/SyncObjImplementer WeaverSyncVarTests.SyncVarsSyncList.SyncVarsSyncList::syncobj)"));
            Assert.That(weaverWarnings, Contains.Item("syncints has [SyncVar] attribute. SyncLists should not be marked with SyncVar (at Mirror.SyncListInt WeaverSyncVarTests.SyncVarsSyncList.SyncVarsSyncList::syncints)"));
        }

        [Test]
        public void SyncVarsMoreThan63()
        {
            Assert.That(weaverErrors, Contains.Item("SyncVarsMoreThan63 has too many SyncVars. Consider refactoring your class into multiple components (at WeaverSyncVarTests.SyncVarsMoreThan63.SyncVarsMoreThan63)"));
        }
    }
}
