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
            Assert.That(weaverErrors, Has.Some.Match(@"Mirror\.Weaver error: Could not find generic arguments for Mirror\.SyncList`1 using MirrorTest\.SomeListInt"));
            Assert.That(weaverErrors, Has.Some.Match(@"Mirror\.Weaver error: Too many generic argument for MirrorTest\.SomeList`2<System.String,System.Int32>"));
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
            string weaverError = @"Mirror\.Weaver error:";
            string fieldType = @"MirrorTest\.SyncListString2 MirrorTest\.SyncListMissingParamlessCtor::Foo";
            string errorMessage = @"Can not intialize field because no default constructor was found\. Manually intialize the field \(call the constructor\) or add constructor without Parameter";
            Assert.That(weaverErrors, Has.Some.Match($"{weaverError} {fieldType} {errorMessage}"));
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
            Assert.That(weaverErrors, Has.Some.Match(@"Mirror\.Weaver error: UnityEngine\.Object MirrorTest\.SomeAbstractClass/MyNestedStruct::target has unsupported type\. Use a type supported by Mirror instead"));
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
            Assert.That(weaverErrors, Has.Some.Match(@"Mirror\.Weaver error: UnityEngine\.Object MirrorTest\.SomeData::target has unsupported type\. Use a type supported by Mirror instead"));
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
            string weaverError = @"Mirror\.Weaver error:";
            string type = @"MirrorTest\.MyGenericStructList";
            string errorMessage = @"Can not create Serialize or Deserialize for generic element\. Override virtual methods with custom Serialize and Deserialize to use MirrorTest.MyGenericStruct`1<System.Single> in SyncList";
            Assert.That(weaverErrors, Has.Some.Match($"{weaverError} {type} {errorMessage}"));
        }

        [Test]
        public void SyncListErrorForGenericStructWithCustomDeserializeOnly()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            string weaverError = @"Mirror\.Weaver error:";
            string type = @"MirrorTest\.MyGenericStructList";
            string errorMessage = @"Can not create Serialize or Deserialize for generic element\. Override virtual methods with custom Serialize and Deserialize to use MirrorTest.MyGenericStruct`1<System.Single> in SyncList";
            Assert.That(weaverErrors, Has.Some.Match($"{weaverError} {type} {errorMessage}"));
        }

        [Test]
        public void SyncListErrorForGenericStructWithCustomSerializeOnly()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            string weaverError = @"Mirror\.Weaver error:";
            string type = @"MirrorTest\.MyGenericStructList";
            string errorMessage = @"Can not create Serialize or Deserialize for generic element\. Override virtual methods with custom Serialize and Deserialize to use MirrorTest.MyGenericStruct`1<System.Single> in SyncList";
            Assert.That(weaverErrors, Has.Some.Match($"{weaverError} {type} {errorMessage}"));
        }

        [Test]
        public void SyncListGenericStructWithCustomMethods()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListErrorWhenUsingGenericListInNetworkBehaviour()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            string weaverError = @"Mirror\.Weaver error:";
            string type = @"MirrorTest\.SomeList`1<System\.Int32> MirrorTest.SyncListErrorWhenUsingGenericListInNetworkBehaviour::someList";
            string errorMessage = @"Can not use generic SyncObjects directly in NetworkBehaviour\. Create a class and inherit from the generic syncList instead\.";
            Assert.That(weaverErrors, Has.Some.Match($"{weaverError} {type} {errorMessage}"));
        }
    }
}
