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
        public void SyncVarArraySegment()
        {
            IsSuccess();
        }

        [Test]
        public void SyncVarsDerivedNetworkBehaviour()
        {
            IsSuccess();
        }

        [Test]
        public void SyncVarsStatic()
        {
            HasError("invalidVar cannot be static",
                "System.Int32 WeaverSyncVarTests.SyncVarsStatic.SyncVarsStatic::invalidVar");
        }

        [Test]
        public void SyncVarsGenericField()
        {
            HasError("invalidVar cannot be synced since it's a generic parameter",
                "T WeaverSyncVarTests.SyncVarGenericFields.SyncVarGenericFields`1::invalidVar");
        }

        [Test]
        public void SyncVarsGenericParam()
        {
            HasError("Cannot generate writer for generic type MySyncVar`1. Use a supported type or provide a custom writer",
                "WeaverSyncVarTests.SyncVarsGenericParam.SyncVarsGenericParam/MySyncVar`1<System.Int32>");
            HasError("invalidVar has unsupported type. Use a supported MirrorNG type instead",
                "WeaverSyncVarTests.SyncVarsGenericParam.SyncVarsGenericParam/MySyncVar`1<System.Int32> WeaverSyncVarTests.SyncVarsGenericParam.SyncVarsGenericParam::invalidVar");
        }

        [Test]
        public void SyncVarsInterface()
        {
            HasError("Cannot generate writer for interface IMySyncVar. Use a supported type or provide a custom writer",
                "WeaverSyncVarTests.SyncVarsInterface.SyncVarsInterface/IMySyncVar");
            HasError("invalidVar has unsupported type. Use a supported MirrorNG type instead",
                "WeaverSyncVarTests.SyncVarsInterface.SyncVarsInterface/IMySyncVar WeaverSyncVarTests.SyncVarsInterface.SyncVarsInterface::invalidVar");
        }

        [Test]
        public void SyncVarsUnityComponent()
        {
            HasError("Cannot generate writer for component type TextMesh. Use a supported type or provide a custom writer",
                "UnityEngine.TextMesh");
            HasError("invalidVar has unsupported type. Use a supported MirrorNG type instead",
                "UnityEngine.TextMesh WeaverSyncVarTests.SyncVarsUnityComponent.SyncVarsUnityComponent::invalidVar");
        }

        [Test]
        public void SyncVarsCantBeArray()
        {
            HasError("thisShouldntWork has invalid type. Use SyncLists instead of arrays",
                "System.Int32[] WeaverSyncVarTests.SyncVarsCantBeArray.SyncVarsCantBeArray::thisShouldntWork");
        }

        //[Test] TODO: Fix me
        //public void SyncVarsSyncList()
        //{
        //    HasNoErrors();
        //    HasWarning("syncobj has [SyncVar] attribute. SyncLists should not be marked with SyncVar",
        //        "WeaverSyncVarTests.SyncVarsSyncList.SyncVarsSyncList/SyncObjImplementer WeaverSyncVarTests.SyncVarsSyncList.SyncVarsSyncList::syncobj");
        //    HasWarning("syncints has [SyncVar] attribute. SyncLists should not be marked with SyncVar",
        //        "Mirror.SyncList`1<System.Int32> WeaverSyncVarTests.SyncVarsSyncList.SyncVarsSyncList::syncints");
        //}

        [Test]
        public void SyncVarsMoreThan63()
        {
            HasError("SyncVarsMoreThan63 has too many SyncVars. Consider refactoring your class into multiple components",
                "WeaverSyncVarTests.SyncVarsMoreThan63.SyncVarsMoreThan63");
        }
    }
}
