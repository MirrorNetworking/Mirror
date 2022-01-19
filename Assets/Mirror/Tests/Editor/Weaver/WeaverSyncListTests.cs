using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncListTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncList()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListByteValid()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListGenericAbstractInheritance()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListGenericInheritance()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListGenericInheritanceWithMultipleGeneric()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListInheritance()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListMissingParamlessCtor()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListMissingParamlessCtorManuallyInitialized()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListNestedStruct()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListNestedInAbstractClass()
        {
            IsSuccess();
        }

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
        public void SyncListNestedInStruct()
        {
            IsSuccess();
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
        public void SyncListStruct()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListErrorForGenericStruct()
        {
            HasError("Cannot generate reader for generic variable MyGenericStruct`1. Use a supported type or provide a custom reader",
                "WeaverSyncListTests.SyncListErrorForGenericStruct.SyncListErrorForGenericStruct/MyGenericStruct`1<System.Single>");
            HasError("Cannot generate writer for generic type MyGenericStruct`1. Use a supported type or provide a custom writer",
                "WeaverSyncListTests.SyncListErrorForGenericStruct.SyncListErrorForGenericStruct/MyGenericStruct`1<System.Single>");
        }

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

        [Test]
        public void SyncListInterfaceWithCustomMethods()
        {
            IsSuccess();
        }

        [Test]
        public void GenericSyncListCanBeUsed()
        {
            IsSuccess();
        }
    }
}
