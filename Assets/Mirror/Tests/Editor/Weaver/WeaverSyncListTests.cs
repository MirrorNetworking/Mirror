using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncListTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncList()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListByteValid()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListGenericAbstractInheritance()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListGenericInheritance()
        {
            Assert.That(weaverErrors, Is.Empty);
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
            Assert.That(weaverErrors, Is.Empty);
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
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListNestedStruct()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListNestedInAbstractClass()
        {
            Assert.That(weaverErrors, Is.Empty);
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
            Assert.That(weaverErrors, Is.Empty);
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
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListStructWithCustomDeserializeOnly()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListStructWithCustomMethods()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListStructWithCustomSerializeOnly()
        {
            Assert.That(weaverErrors, Is.Empty);
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
            Assert.That(weaverErrors, Is.Empty);
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
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListInheritanceWithOverrides()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListErrorWhenUsingGenericListInNetworkBehaviour()
        {
            HasError("Cannot use generic SyncObject someList directly in NetworkBehaviour. Create a class and inherit from the generic SyncObject instead",
                "WeaverSyncListTests.SyncListErrorWhenUsingGenericListInNetworkBehaviour.SyncListErrorWhenUsingGenericListInNetworkBehaviour/SomeList`1<System.Int32> WeaverSyncListTests.SyncListErrorWhenUsingGenericListInNetworkBehaviour.SyncListErrorWhenUsingGenericListInNetworkBehaviour::someList");
        }
    }
}
