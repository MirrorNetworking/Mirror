using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    // Some tests for SyncObjects are in WeaverSyncListTests and apply to SyncDictionary too
    public class WeaverSyncDictionaryTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncDictionary()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncDictionaryGenericAbstractInheritance()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncDictionaryGenericInheritance()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncDictionaryInheritance()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncDictionaryStructKey()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncDictionaryStructItem()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncDictionaryStructKeyWithCustomDeserializeOnly()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncDictionaryStructItemWithCustomDeserializeOnly()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncDictionaryStructKeyWithCustomMethods()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncDictionaryStructItemWithCustomMethods()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncDictionaryStructKeyWithCustomSerializeOnly()
        {
            Assert.That(weaverErrors, Is.Empty);
        }


        [Test]
        public void SyncDictionaryStructItemWithCustomSerializeOnly()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncDictionaryErrorForGenericStructKey()
        {
            Assert.That(weaverErrors, Contains.Item("Can not create Serialize or Deserialize for generic element in MyGenericStructDictionary. Override virtual methods with custom Serialize and Deserialize to use MirrorTest.SyncDictionaryErrorForGenericStructKey/MyGenericStruct`1<System.Single> in SyncList (at MirrorTest.SyncDictionaryErrorForGenericStructKey/MyGenericStructDictionary)"));
        }

        [Test]
        public void SyncDictionaryErrorForGenericStructItem()
        {
            Assert.That(weaverErrors, Contains.Item("Can not create Serialize or Deserialize for generic element in MyGenericStructDictionary. Override virtual methods with custom Serialize and Deserialize to use MirrorTest.SyncDictionaryErrorForGenericStructItem/MyGenericStruct`1<System.Single> in SyncList (at MirrorTest.SyncDictionaryErrorForGenericStructItem/MyGenericStructDictionary)"));
        }

        [Test]
        public void SyncDictionaryErrorForGenericStructKeyWithCustomDeserializeOnly()
        {
            Assert.That(weaverErrors, Contains.Item("Can not create Serialize or Deserialize for generic element in MyGenericStructDictionary. Override virtual methods with custom Serialize and Deserialize to use MirrorTest.SyncDictionaryErrorForGenericStructKeyWithCustomDeserializeOnly/MyGenericStruct`1<System.Single> in SyncList (at MirrorTest.SyncDictionaryErrorForGenericStructKeyWithCustomDeserializeOnly/MyGenericStructDictionary)"));
        }

        [Test]
        public void SyncDictionaryErrorForGenericStructItemWithCustomDeserializeOnly()
        {
            Assert.That(weaverErrors, Contains.Item("Can not create Serialize or Deserialize for generic element in MyGenericStructDictionary. Override virtual methods with custom Serialize and Deserialize to use MirrorTest.SyncDictionaryErrorForGenericStructItemWithCustomDeserializeOnly/MyGenericStruct`1<System.Single> in SyncList (at MirrorTest.SyncDictionaryErrorForGenericStructItemWithCustomDeserializeOnly/MyGenericStructDictionary)"));
        }

        [Test]
        public void SyncDictionaryErrorForGenericStructKeyWithCustomSerializeOnly()
        {
            Assert.That(weaverErrors, Contains.Item("Can not create Serialize or Deserialize for generic element in MyGenericStructDictionary. Override virtual methods with custom Serialize and Deserialize to use MyGenericStruct`1 in SyncList (at MirrorTest.SyncDictionaryErrorForGenericStructKeyWithCustomSerializeOnly/MyGenericStructDictionary)"));
        }

        [Test]
        public void SyncDictionaryErrorForGenericStructItemWithCustomSerializeOnly()
        {
            Assert.That(weaverErrors, Contains.Item("Can not create Serialize or Deserialize for generic element in MyGenericStructDictionary. Override virtual methods with custom Serialize and Deserialize to use MyGenericStruct`1 in SyncList (at MirrorTest.SyncDictionaryErrorForGenericStructItemWithCustomSerializeOnly/MyGenericStructDictionary)"));
        }

        [Test]
        public void SyncDictionaryGenericStructKeyWithCustomMethods()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncDictionaryGenericStructItemWithCustomMethods()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncDictionaryErrorWhenUsingGenericInNetworkBehaviour()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot use generic SyncObject someDictionary directly in NetworkBehaviour. Create a class and inherit from the generic SyncObject instead (at MirrorTest.SyncDictionaryErrorWhenUsingGenericInNetworkBehaviour/SomeSyncDictionary`2<System.Int32,System.String> MirrorTest.SyncDictionaryErrorWhenUsingGenericInNetworkBehaviour::someDictionary)"));
        }
    }
}
