using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncVarAttributeTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncVarsStatic()
        {
            HasError("invalidVar cannot be static",
                "System.Int32 WeaverSyncVarTests.SyncVarsStatic.SyncVarsStatic::invalidVar");
        }

        [Test]
        public void SyncVarsGenericParam()
        {
            HasError("Cannot generate writer for generic type MySyncVar`1. Use a supported type or provide a custom writer",
                "WeaverSyncVarTests.SyncVarsGenericParam.SyncVarsGenericParam/MySyncVar`1<System.Int32>");
            HasError("invalidVar has unsupported type. Use a supported Mirror type instead",
                "WeaverSyncVarTests.SyncVarsGenericParam.SyncVarsGenericParam/MySyncVar`1<System.Int32> WeaverSyncVarTests.SyncVarsGenericParam.SyncVarsGenericParam::invalidVar");
        }

        [Test]
        public void SyncVarsInterface()
        {
            HasError("Cannot generate writer for interface IMySyncVar. Use a supported type or provide a custom writer",
                "WeaverSyncVarTests.SyncVarsInterface.SyncVarsInterface/IMySyncVar");
            HasError("invalidVar has unsupported type. Use a supported Mirror type instead",
                "WeaverSyncVarTests.SyncVarsInterface.SyncVarsInterface/IMySyncVar WeaverSyncVarTests.SyncVarsInterface.SyncVarsInterface::invalidVar");
        }

        [Test]
        public void SyncVarsUnityComponent()
        {
            HasError("Cannot generate writer for component type TextMesh. Use a supported type or provide a custom writer",
                "UnityEngine.TextMesh");
            HasError("invalidVar has unsupported type. Use a supported Mirror type instead",
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
            // NOTE if this test fails without a warning:
            // that happens if after WeaverAssembler->AssemblyBuilder.Build(),
            // Unity invokes ILPostProcessor internally.
            // and we invoke it from WeaverAssembler buildFinished again.
            // => make sure that our ILPostProcessor does nto run on
            //    WeaverAssembler assemblies
            HasNoErrors();
            HasWarning("syncobj has [SyncVar] attribute. SyncLists should not be marked with SyncVar",
                "WeaverSyncVarTests.SyncVarsSyncList.SyncVarsSyncList/SyncObjImplementer WeaverSyncVarTests.SyncVarsSyncList.SyncVarsSyncList::syncobj");
            HasWarning("syncints has [SyncVar] attribute. SyncLists should not be marked with SyncVar",
                "Mirror.SyncList`1<System.Int32> WeaverSyncVarTests.SyncVarsSyncList.SyncVarsSyncList::syncints");
        }

        [Test]
        public void SyncVarsMoreThanMax()
        {
            HasError("SyncVarsMoreThanMax has > 64 SyncVars. Consider refactoring your class into multiple components",
                "WeaverSyncVarTests.SyncVarsMoreThanMax.SyncVarsMoreThanMax");
        }
    }
}
