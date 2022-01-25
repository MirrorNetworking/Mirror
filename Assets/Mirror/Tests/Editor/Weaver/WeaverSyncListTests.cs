using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncListTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncListNestedInAbstractClassWithInvalid()
        {
            // we need this negative test to make sure that SyncList is being processed
            HasError("Cannot generate reader for Object. Use a supported type or provide a custom reader",
                "UnityEngine.Object");
            HasError("target has an unsupported type",
                "UnityEngine.Object WeaverSyncListTests.SyncListNestedInAbstractClassWithInvalid.SyncListNestedStructWithInvalid/SomeAbstractClass/MyNestedStruct::target");
            HasError("Cannot generate writer for Object. Use a supported type or provide a custom writer",
                "UnityEngine.Object");
        }

        [Test]
        public void SyncListNestedInStructWithInvalid()
        {
            // we need this negative test to make sure that SyncList is being processed
            HasError("Cannot generate reader for Object. Use a supported type or provide a custom reader",
                "UnityEngine.Object");
            HasError("target has an unsupported type",
                "UnityEngine.Object WeaverSyncListTests.SyncListNestedInStructWithInvalid.SyncListNestedInStructWithInvalid/SomeData::target");
            HasError("Cannot generate writer for Object. Use a supported type or provide a custom writer",
                "UnityEngine.Object");
        }

        [Test]
        public void SyncListErrorForGenericStruct()
        {
            HasError("Cannot generate reader for generic variable MyGenericStruct`1. Use a supported type or provide a custom reader",
                "WeaverSyncListTests.SyncListErrorForGenericStruct.SyncListErrorForGenericStruct/MyGenericStruct`1<System.Single>");
            HasError("Cannot generate writer for generic type MyGenericStruct`1. Use a supported type or provide a custom writer",
                "WeaverSyncListTests.SyncListErrorForGenericStruct.SyncListErrorForGenericStruct/MyGenericStruct`1<System.Single>");
        }

        // IsSuccess test, but still in here because it shows an error
        // if we move to regular C#
        [Test]
        public void SyncListGenericStructWithCustomMethods()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListErrorForInterface()
        {
            HasError("Cannot generate reader for interface MyInterface. Use a supported type or provide a custom reader",
                "WeaverSyncListTests.SyncListErrorForInterface.MyInterface");
            HasError("Cannot generate writer for interface MyInterface. Use a supported type or provide a custom writer",
                "WeaverSyncListTests.SyncListErrorForInterface.MyInterface");
        }

        // IsSuccess test, but still in here because it shows an error
        // if we move to regular C#
        [Test]
        public void SyncListInterfaceWithCustomMethods()
        {
            IsSuccess();
        }
    }
}
