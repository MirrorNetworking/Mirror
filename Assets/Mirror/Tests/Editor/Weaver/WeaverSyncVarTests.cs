using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncVarTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncVarsValid()
        {
            IsSuccess();
        }

        [Test]
        public void SyncVarsDerivedNetworkBehaviour()
        {
            HasError("Invalid SyncVar: Cannot generate writer for component type MyBehaviour",
                "WeaverSyncVarTests.SyncVarsDerivedNetworkBehaviour.MyBehaviour WeaverSyncVarTests.SyncVarsDerivedNetworkBehaviour.SyncVarsDerivedNetworkBehaviour::invalidVar");
        }

        [Test]
        public void SyncVarsStatic()
        {
            HasError("invalidVar cannot be static",
                "System.Int32 WeaverSyncVarTests.SyncVarsStatic.SyncVarsStatic::invalidVar");
        }

        [Test]
        public void SyncVarsGenericParam()
        {
            HasError("Invalid SyncVar: Cannot generate writer for generic type MySyncVar`1",
                "WeaverSyncVarTests.SyncVarsGenericParam.SyncVarsGenericParam/MySyncVar`1<System.Int32> WeaverSyncVarTests.SyncVarsGenericParam.SyncVarsGenericParam::invalidVar");
        }

        [Test]
        public void SyncVarsInterface()
        {
            HasError("Invalid SyncVar: Cannot generate writer for interface IMySyncVar",
               "WeaverSyncVarTests.SyncVarsInterface.SyncVarsInterface/IMySyncVar WeaverSyncVarTests.SyncVarsInterface.SyncVarsInterface::invalidVar");
        }

        [Test]
        public void SyncVarsUnityComponent()
        {
            HasError("Invalid SyncVar: Cannot generate writer for component type TextMesh",
                "UnityEngine.TextMesh WeaverSyncVarTests.SyncVarsUnityComponent.SyncVarsUnityComponent::invalidVar");
        }

        [Test]
        public void SyncVarsCantBeArray()
        {
            HasError("thisShouldntWork has invalid type. Use SyncLists instead of arrays",
                "System.Int32[] WeaverSyncVarTests.SyncVarsCantBeArray.SyncVarsCantBeArray::thisShouldntWork");
        }

        [Test]
        public void SyncVarsSyncList()
        {
            HasNoErrors();
            HasWarning("syncobj has [SyncVar] attribute. SyncLists should not be marked with SyncVar",
                "WeaverSyncVarTests.SyncVarsSyncList.SyncVarsSyncList/SyncObjImplementer WeaverSyncVarTests.SyncVarsSyncList.SyncVarsSyncList::syncobj");
            HasWarning("syncints has [SyncVar] attribute. SyncLists should not be marked with SyncVar",
                "Mirror.SyncListInt WeaverSyncVarTests.SyncVarsSyncList.SyncVarsSyncList::syncints");
        }

        [Test]
        public void SyncVarsMoreThan63()
        {
            HasError("SyncVarsMoreThan63 has too many SyncVars. Consider refactoring your class into multiple components",
                "WeaverSyncVarTests.SyncVarsMoreThan63.SyncVarsMoreThan63");
        }
    }
}
