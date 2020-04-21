using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncListTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncList()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListByteValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListGenericAbstractInheritance()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListGenericInheritance()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListGenericInheritanceWithMultipleGeneric()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Too many generic argument for MirrorTest.SyncListGenericInheritanceWithMultipleGeneric/SomeList`2<System.String,System.Int32>"));
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Could not find generic arguments for Mirror.SyncList`1 using MirrorTest.SyncListGenericInheritanceWithMultipleGeneric/SomeListInt"));
        }

        [Test]
        public void SyncListInheritance()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListMissingParamlessCtor()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.SyncListMissingParamlessCtor/SyncListString2 MirrorTest.SyncListMissingParamlessCtor::Foo Can not intialize field because no default constructor was found. Manually intialize the field (call the constructor) or add constructor without Parameter"));
        }

        [Test]
        public void SyncListMissingParamlessCtorManuallyInitialized()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListNestedStruct()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListNestedInAbstractClass()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListNestedInAbstractClassWithInvalid()
        {
            // we need this negative test to make sure that SyncList is being processed 
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot generate writer for UnityEngine.Object. Use a supported type or provide a custom writer"));
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: UnityEngine.Object MirrorTest.SyncListNestedStructWithInvalid/SomeAbstractClass/MyNestedStruct::target has unsupported type. Use a type supported by Mirror instead"));
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.SyncListNestedStructWithInvalid/SomeAbstractClass/MyNestedStructList cannot have item of type MirrorTest.SyncListNestedStructWithInvalid/SomeAbstractClass/MyNestedStruct.  Use a type supported by mirror instead"));
        }

        [Test]
        public void SyncListNestedInStruct()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListNestedInStructWithInvalid()
        {
            // we need this negative test to make sure that SyncList is being processed 
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot generate writer for UnityEngine.Object. Use a supported type or provide a custom writer"));
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: UnityEngine.Object MirrorTest.SyncListNestedInStructWithInvalid/SomeData::target has unsupported type. Use a type supported by Mirror instead"));
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.SyncListNestedInStructWithInvalid/SomeData/SyncList cannot have item of type MirrorTest.SyncListNestedInStructWithInvalid/SomeData.  Use a type supported by mirror instead"));
        }

        [Test]
        public void SyncListStruct()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListStructWithCustomDeserializeOnly()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListStructWithCustomMethods()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListStructWithCustomSerializeOnly()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListErrorForGenericStruct()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.SyncListErrorForGenericStruct/MyGenericStructList Can not create Serialize or Deserialize for generic element. Override virtual methods with custom Serialize and Deserialize to use MirrorTest.SyncListErrorForGenericStruct/MyGenericStruct`1<System.Single> in SyncList"));
        }

        [Test]
        public void SyncListErrorForGenericStructWithCustomDeserializeOnly()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.SyncListErrorForGenericStructWithCustomDeserializeOnly/MyGenericStructList Can not create Serialize or Deserialize for generic element. Override virtual methods with custom Serialize and Deserialize to use MirrorTest.SyncListErrorForGenericStructWithCustomDeserializeOnly/MyGenericStruct`1<System.Single> in SyncList"));
        }

        [Test]
        public void SyncListErrorForGenericStructWithCustomSerializeOnly()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.SyncListErrorForGenericStructWithCustomSerializeOnly/MyGenericStructList Can not create Serialize or Deserialize for generic element. Override virtual methods with custom Serialize and Deserialize to use MirrorTest.SyncListErrorForGenericStructWithCustomSerializeOnly/MyGenericStruct`1<System.Single> in SyncList"));
        }

        [Test]
        public void SyncListGenericStructWithCustomMethods()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListErrorForInterface()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            string weaverError = @"Mirror\.Weaver error:";
            string type = @"MirrorTest\.MyInterfaceList";
            string errorMessage = @"cannot have item of type MirrorTest\.MyInterface\.  Use a type supported by mirror instead";
            Assert.That(weaverErrors, Has.Some.Match($"{weaverError} {type} {errorMessage}"));
        }

        [Test]
        public void SyncListInterfaceWithCustomMethods()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListInheritanceWithOverrides()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListErrorWhenUsingGenericListInNetworkBehaviour()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.SyncListErrorWhenUsingGenericListInNetworkBehaviour/SomeList`1<System.Int32> MirrorTest.SyncListErrorWhenUsingGenericListInNetworkBehaviour::someList Can not use generic SyncObjects directly in NetworkBehaviour. Create a class and inherit from the generic SyncObject instead."));
        }
    }
}
