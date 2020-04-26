using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncListTests : BatchedWeaverTests
    {
        [BatchedTest(true)]
        public void SyncList()
        {
            Assert.Pass();
        }

        [BatchedTest(true)]
        public void SyncListByteValid()
        {
            Assert.Pass();
        }

        [BatchedTest(true)]
        public void SyncListGenericAbstractInheritance()
        {
            Assert.Pass();
        }

        [BatchedTest(true)]
        public void SyncListGenericInheritance()
        {
            Assert.Pass();
        }

        [BatchedTest(false)]
        public void SyncListErrorForGenericInheritanceWithMultipleGeneric()
        {
            AssertHasError("Could not find generic arguments for SyncList`1 in SyncListErrorForGenericInheritanceWithMultipleGeneric.SomeListInt (at SyncListErrorForGenericInheritanceWithMultipleGeneric.SomeListInt)");
            AssertHasError("Type SomeListInt has too many generic arguments in base class SyncListErrorForGenericInheritanceWithMultipleGeneric.SomeList`2<System.String,System.Int32> (at SyncListErrorForGenericInheritanceWithMultipleGeneric.SomeListInt)");
        }

        [BatchedTest(true)]
        public void SyncListInheritance()
        {
            Assert.Pass();
        }

        [BatchedTest(false)]
        public void SyncListErrorForMissingParamlessCtor()
        {
            AssertHasError("Can not intialize field Foo because no default constructor was found. Manually intialize the field (call the constructor) or add constructor without Parameter (at SyncListErrorForMissingParamlessCtor.SyncListString2 SyncListErrorForMissingParamlessCtor.MyBehaviour::Foo)");
        }

        [BatchedTest(true)]
        public void SyncListMissingParamlessCtorManuallyInitialized()
        {
            Assert.Pass();
        }

        [BatchedTest(true)]
        public void SyncListNestedStruct()
        {
            Assert.Pass();
        }

        [BatchedTest(true)]
        public void SyncListNestedInAbstractClass()
        {
            Assert.Pass();
        }

        [BatchedTest(false)]
        public void SyncListErrorForNestedInAbstractClassWithInvalid()
        {
            // we need this negative test to make sure that SyncList is being processed 
            AssertHasError("MyNestedStructList has sync object generic type MyNestedStruct.  Use a type supported by mirror instead (at SyncListErrorForNestedInAbstractClassWithInvalid.SomeAbstractClass/MyNestedStructList)");
        }

        [BatchedTest(true)]
        public void SyncListNestedInStruct()
        {
            Assert.Pass();
        }

        [BatchedTest(false)]
        public void SyncListErrorForNestedInStructWithInvalid()
        {
            // we need this negative test to make sure that SyncList is being processed 
            AssertHasError("SyncList has sync object generic type SomeData.  Use a type supported by mirror instead (at SyncListErrorForNestedInStructWithInvalid.SomeData/SyncList)");
        }

        [BatchedTest(true)]
        public void SyncListStruct()
        {
            Assert.Pass();
        }

        [BatchedTest(true)]
        public void SyncListStructWithCustomDeserializeOnly()
        {
            Assert.Pass();
        }

        [BatchedTest(true)]
        public void SyncListStructWithCustomMethods()
        {
            Assert.Pass();
        }

        [BatchedTest(true)]
        public void SyncListStructWithCustomSerializeOnly()
        {
            Assert.Pass();
        }

        [BatchedTest(false)]
        public void SyncListErrorForGenericStruct()
        {
            AssertHasError("Can not create Serialize or Deserialize for generic element in MyGenericStructList. Override virtual methods with custom Serialize and Deserialize to use SyncListErrorForGenericStruct.MyGenericStruct`1<System.Single> in SyncList (at SyncListErrorForGenericStruct.MyGenericStructList)");
        }

        [BatchedTest(false)]
        public void SyncListErrorForGenericStructWithCustomDeserializeOnly()
        {
            AssertHasError("Can not create Serialize or Deserialize for generic element in MyGenericStructList. Override virtual methods with custom Serialize and Deserialize to use SyncListErrorForGenericStructWithCustomDeserializeOnly.MyGenericStruct`1<System.Single> in SyncList (at SyncListErrorForGenericStructWithCustomDeserializeOnly.MyGenericStructList)");
        }

        [BatchedTest(false)]
        public void SyncListErrorForGenericStructWithCustomSerializeOnly()
        {
            AssertHasError("Can not create Serialize or Deserialize for generic element in MyGenericStructList. Override virtual methods with custom Serialize and Deserialize to use MyGenericStruct`1 in SyncList (at SyncListErrorForGenericStructWithCustomSerializeOnly.MyGenericStructList)");
        }

        [BatchedTest(true)]
        public void SyncListGenericStructWithCustomMethods()
        {
            Assert.Pass();
        }

        [BatchedTest(false)]
        public void SyncListErrorForInterface()
        {
            AssertHasError("Cannot generate writer for interface MyInterface. Use a supported type or provide a custom writer (at MirrorTest.MyInterface)");
            AssertHasError("MyInterfaceList has sync object generic type MyInterface.  Use a type supported by mirror instead (at MirrorTest.MyInterfaceList)");
        }

        [BatchedTest(true)]
        public void SyncListInterfaceWithCustomMethods()
        {
            Assert.Pass();
        }

        [BatchedTest(true)]
        public void SyncListInheritanceWithOverrides()
        {
            Assert.Pass();
        }

        [BatchedTest(false)]
        public void SyncListErrorWhenUsingGenericListInNetworkBehaviour()
        {
            AssertHasError("Cannot use generic SyncObject someList directly in NetworkBehaviour. Create a class and inherit from the generic SyncObject instead (at SyncListErrorWhenUsingGenericListInNetworkBehaviour.SomeList`1<System.Int32> SyncListErrorWhenUsingGenericListInNetworkBehaviour.MyBehaviour::someList)");
        }
    }
}
