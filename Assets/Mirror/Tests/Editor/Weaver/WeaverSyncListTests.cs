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
            Assert.That(weaverErrors, Contains.Item("Could not find generic arguments for SyncList`1 in MirrorTest.SyncListGenericInheritanceWithMultipleGeneric/SomeListInt (at MirrorTest.SyncListGenericInheritanceWithMultipleGeneric/SomeListInt)"));
        }

        [Test]
        public void SyncListInheritance()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListMissingParamlessCtor()
        {
            Assert.That(weaverErrors, Contains.Item("Can not intialize field Foo because no default constructor was found. Manually intialize the field (call the constructor) or add constructor without Parameter (at MirrorTest.SyncListMissingParamlessCtor/SyncListString2 MirrorTest.SyncListMissingParamlessCtor::Foo)"));
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
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for Object. Use a supported type or provide a custom writer (at UnityEngine.Object)"));
            Assert.That(weaverErrors, Contains.Item("target has unsupported type. Use a type supported by Mirror instead (at UnityEngine.Object MirrorTest.SyncListNestedStructWithInvalid/SomeAbstractClass/MyNestedStruct::target)"));
            Assert.That(weaverErrors, Contains.Item("MyNestedStructList has sync object generic type MyNestedStruct.  Use a type supported by mirror instead (at MirrorTest.SyncListNestedStructWithInvalid/SomeAbstractClass/MyNestedStructList)"));
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
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for Object. Use a supported type or provide a custom writer (at UnityEngine.Object)"));
            Assert.That(weaverErrors, Contains.Item("target has unsupported type. Use a type supported by Mirror instead (at UnityEngine.Object MirrorTest.SyncListNestedInStructWithInvalid/SomeData::target)"));
            Assert.That(weaverErrors, Contains.Item("SyncList has sync object generic type SomeData.  Use a type supported by mirror instead (at MirrorTest.SyncListNestedInStructWithInvalid/SomeData/SyncList)"));
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
            Assert.That(weaverErrors, Contains.Item("Can not create Serialize or Deserialize for generic element in MyGenericStructList. Override virtual methods with custom Serialize and Deserialize to use MirrorTest.SyncListErrorForGenericStruct/MyGenericStruct`1<System.Single> in SyncList (at MirrorTest.SyncListErrorForGenericStruct/MyGenericStructList)"));
        }

        [Test]
        public void SyncListErrorForGenericStructWithCustomDeserializeOnly()
        {
            Assert.That(weaverErrors, Contains.Item("Can not create Serialize or Deserialize for generic element in MyGenericStructList. Override virtual methods with custom Serialize and Deserialize to use MirrorTest.SyncListErrorForGenericStructWithCustomDeserializeOnly/MyGenericStruct`1<System.Single> in SyncList (at MirrorTest.SyncListErrorForGenericStructWithCustomDeserializeOnly/MyGenericStructList)"));
        }

        [Test]
        public void SyncListErrorForGenericStructWithCustomSerializeOnly()
        {
            Assert.That(weaverErrors, Contains.Item("Can not create Serialize or Deserialize for generic element in MyGenericStructList. Override virtual methods with custom Serialize and Deserialize to use MyGenericStruct`1 in SyncList (at MirrorTest.SyncListErrorForGenericStructWithCustomSerializeOnly/MyGenericStructList)"));
        }

        [Test]
        public void SyncListGenericStructWithCustomMethods()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListErrorForInterface()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for interface MyInterface. Use a supported type or provide a custom writer (at MirrorTest.MyInterface)"));
            Assert.That(weaverErrors, Contains.Item("MyInterfaceList has sync object generic type MyInterface.  Use a type supported by mirror instead (at MirrorTest.MyInterfaceList)"));
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
            Assert.That(weaverErrors, Contains.Item("Cannot use generic SyncObject someList directly in NetworkBehaviour. Create a class and inherit from the generic SyncObject instead (at MirrorTest.SyncListErrorWhenUsingGenericListInNetworkBehaviour/SomeList`1<System.Int32> MirrorTest.SyncListErrorWhenUsingGenericListInNetworkBehaviour::someList)"));
        }
    }
}
