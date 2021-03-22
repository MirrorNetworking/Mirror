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
            HasError("Could not find generic arguments for SyncList`1 in WeaverSyncListTests.SyncListGenericInheritanceWithMultipleGeneric.SyncListGenericInheritanceWithMultipleGeneric/SomeListInt",
                "WeaverSyncListTests.SyncListGenericInheritanceWithMultipleGeneric.SyncListGenericInheritanceWithMultipleGeneric/SomeListInt");
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
            HasError("Cannot generate writer for Object. Use a supported type or provide a custom writer",
                "UnityEngine.Object");
            HasError("target has unsupported type. Use a type supported by Mirror instead",
                "UnityEngine.Object WeaverSyncListTests.SyncListNestedInAbstractClassWithInvalid.SyncListNestedStructWithInvalid/SomeAbstractClass/MyNestedStruct::target");
            HasError("MyNestedStructList has sync object generic type MyNestedStruct.  Use a type supported by mirror instead",
                "WeaverSyncListTests.SyncListNestedInAbstractClassWithInvalid.SyncListNestedStructWithInvalid/SomeAbstractClass/MyNestedStructList");
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
            HasError("Cannot generate writer for Object. Use a supported type or provide a custom writer",
                "UnityEngine.Object");
            HasError("target has unsupported type. Use a type supported by Mirror instead",
                "UnityEngine.Object WeaverSyncListTests.SyncListNestedInStructWithInvalid.SyncListNestedInStructWithInvalid/SomeData::target");
            HasError("SyncList has sync object generic type SomeData.  Use a type supported by mirror instead",
                "WeaverSyncListTests.SyncListNestedInStructWithInvalid.SyncListNestedInStructWithInvalid/SomeData/SyncList");
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
            HasError("Can not create Serialize or Deserialize for generic element in MyGenericStructList. Override virtual methods with custom Serialize and Deserialize to use WeaverSyncListTests.SyncListErrorForGenericStruct.SyncListErrorForGenericStruct/MyGenericStruct`1<System.Single> in SyncList",
                "WeaverSyncListTests.SyncListErrorForGenericStruct.SyncListErrorForGenericStruct/MyGenericStructList");
        }

        [Test]
        public void SyncListErrorForGenericStructWithCustomDeserializeOnly()
        {
            HasError("Can not create Serialize or Deserialize for generic element in MyGenericStructList. Override virtual methods with custom Serialize and Deserialize to use WeaverSyncListTests.SyncListErrorForGenericStructWithCustomDeserializeOnly.SyncListErrorForGenericStructWithCustomDeserializeOnly/MyGenericStruct`1<System.Single> in SyncList",
                "WeaverSyncListTests.SyncListErrorForGenericStructWithCustomDeserializeOnly.SyncListErrorForGenericStructWithCustomDeserializeOnly/MyGenericStructList");
        }

        [Test]
        public void SyncListErrorForGenericStructWithCustomSerializeOnly()
        {
            HasError("Can not create Serialize or Deserialize for generic element in MyGenericStructList. Override virtual methods with custom Serialize and Deserialize to use MyGenericStruct`1 in SyncList",
                "WeaverSyncListTests.SyncListErrorForGenericStructWithCustomSerializeOnly.SyncListErrorForGenericStructWithCustomSerializeOnly/MyGenericStructList");
        }

        [Test]
        public void SyncListGenericStructWithCustomMethods()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListErrorForInterface()
        {
            HasError("Cannot generate writer for interface MyInterface. Use a supported type or provide a custom writer",
                "WeaverSyncListTests.SyncListErrorForInterface.MyInterface");
            HasError("MyInterfaceList has sync object generic type MyInterface.  Use a type supported by mirror instead",
                "WeaverSyncListTests.SyncListErrorForInterface.MyInterfaceList");
        }

        [Test]
        public void SyncListInterfaceWithCustomMethods()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListInheritanceWithOverrides()
        {
            IsSuccess();
        }

        [Test]
        public void SyncListErrorWhenUsingGenericListInNetworkBehaviour()
        {
            HasError("Cannot use generic SyncObject someList directly in NetworkBehaviour. Create a class and inherit from the generic SyncObject instead",
                "WeaverSyncListTests.SyncListErrorWhenUsingGenericListInNetworkBehaviour.SyncListErrorWhenUsingGenericListInNetworkBehaviour/SomeList`1<System.Int32> WeaverSyncListTests.SyncListErrorWhenUsingGenericListInNetworkBehaviour.SyncListErrorWhenUsingGenericListInNetworkBehaviour::someList");
        }
    }
}
