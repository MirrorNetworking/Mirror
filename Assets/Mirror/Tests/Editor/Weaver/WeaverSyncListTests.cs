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
            HasError("Can not initialize field Foo because no default constructor was found. Manually initialize the field (call the constructor) or add constructor without Parameter",
                "WeaverSyncListTests.SyncListMissingParamlessCtor.SyncListMissingParamlessCtor/SyncListString2 WeaverSyncListTests.SyncListMissingParamlessCtor.SyncListMissingParamlessCtor::Foo");
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
        public void SyncListStructWithCustomDeserializeOnly()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListStructWithCustomMethods()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListStructWithCustomSerializeOnly()
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
        public void SyncListErrorForGenericStructWithCustomDeserializeOnly()
        {
            HasError("Cannot generate reader for generic variable MyGenericStruct`1. Use a supported type or provide a custom reader",
                "WeaverSyncListTests.SyncListErrorForGenericStructWithCustomDeserializeOnly.SyncListErrorForGenericStructWithCustomDeserializeOnly/MyGenericStruct`1<System.Single>");
            HasError("Cannot generate writer for generic type MyGenericStruct`1. Use a supported type or provide a custom writer",
                "WeaverSyncListTests.SyncListErrorForGenericStructWithCustomDeserializeOnly.SyncListErrorForGenericStructWithCustomDeserializeOnly/MyGenericStruct`1<System.Single>");
        }

        [Test]
        public void SyncListErrorForGenericStructWithCustomSerializeOnly()
        {
            HasError("Cannot generate reader for generic variable MyGenericStruct`1. Use a supported type or provide a custom reader",
                "WeaverSyncListTests.SyncListErrorForGenericStructWithCustomSerializeOnly.SyncListErrorForGenericStructWithCustomSerializeOnly/MyGenericStruct`1<System.Single>");
            HasError("Cannot generate writer for generic type MyGenericStruct`1. Use a supported type or provide a custom writer",
                "WeaverSyncListTests.SyncListErrorForGenericStructWithCustomSerializeOnly.SyncListErrorForGenericStructWithCustomSerializeOnly/MyGenericStruct`1<System.Single>");
        }

        [Test]
        public void SyncListGenericStructWithCustomMethods()
        {
            HasError("Cannot generate reader for generic variable MyGenericStruct`1. Use a supported type or provide a custom reader",
                "WeaverSyncListTests.SyncListGenericStructWithCustomMethods.SyncListGenericStructWithCustomMethods/MyGenericStruct`1<System.Single>");
            HasError("Cannot generate writer for generic type MyGenericStruct`1. Use a supported type or provide a custom writer",
                "WeaverSyncListTests.SyncListGenericStructWithCustomMethods.SyncListGenericStructWithCustomMethods/MyGenericStruct`1<System.Single>");
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
            HasError("Cannot generate reader for interface IMyInterface. Use a supported type or provide a custom reader",
                "WeaverSyncListTests.SyncListInterfaceWithCustomMethods.IMyInterface");
            HasError("Cannot generate writer for interface IMyInterface. Use a supported type or provide a custom writer",
                "WeaverSyncListTests.SyncListInterfaceWithCustomMethods.IMyInterface");
        }

        [Test]
        public void SyncListInheritanceWithOverrides()
        {
            HasError("Cannot generate reader for component type MyBehaviourWithValue. Use a supported type or provide a custom reader",
                "WeaverSyncListTests.SyncListInheritanceWithOverrides.MyBehaviourWithValue");
            HasError("Cannot generate writer for component type MyBehaviourWithValue. Use a supported type or provide a custom writer",
                "WeaverSyncListTests.SyncListInheritanceWithOverrides.MyBehaviourWithValue");
        }

        [Test]
        public void SyncListErrorWhenUsingGenericListInNetworkBehaviour()
        {
            IsSuccess();
        }
    }
}
