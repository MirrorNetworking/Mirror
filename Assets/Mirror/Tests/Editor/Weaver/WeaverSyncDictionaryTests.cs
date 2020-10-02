using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    // Some tests for SyncObjects are in WeaverSyncListTests and apply to SyncDictionary too
    public class WeaverSyncDictionaryTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncDictionary()
        {
            IsSuccess();
        }

        [Test]
        public void SyncDictionaryGenericAbstractInheritance()
        {
            IsSuccess();
        }

        [Test]
        public void SyncDictionaryGenericInheritance()
        {
            IsSuccess();
        }

        [Test]
        public void SyncDictionaryInheritance()
        {
            IsSuccess();
        }

        [Test]
        public void SyncDictionaryStructKey()
        {
            IsSuccess();
        }

        [Test]
        public void SyncDictionaryStructItem()
        {
            IsSuccess();
        }

        [Test]
        public void SyncDictionaryStructKeyWithCustomDeserializeOnly()
        {
            IsSuccess();
        }

        [Test]
        public void SyncDictionaryStructItemWithCustomDeserializeOnly()
        {
            IsSuccess();
        }

        [Test]
        public void SyncDictionaryStructKeyWithCustomMethods()
        {
            IsSuccess();
        }

        [Test]
        public void SyncDictionaryStructItemWithCustomMethods()
        {
            IsSuccess();
        }

        [Test]
        public void SyncDictionaryStructKeyWithCustomSerializeOnly()
        {
            IsSuccess();
        }


        [Test]
        public void SyncDictionaryStructItemWithCustomSerializeOnly()
        {
            IsSuccess();
        }

        [Test]
        public void SyncDictionaryErrorForGenericStructKey()
        {
            HasError("Cannot generate reader for generic variable MyGenericStruct`1. Use a supported type or provide a custom reader",
                "WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructKey.SyncDictionaryErrorForGenericStructKey/MyGenericStruct`1<System.Single>");
            HasError("Cannot generate writer for generic type MyGenericStruct`1. Use a supported type or provide a custom writer",
                "WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructKey.SyncDictionaryErrorForGenericStructKey/MyGenericStruct`1<System.Single>");
        }

        [Test]
        public void SyncDictionaryErrorForGenericStructItem()
        {
            HasError("Cannot generate reader for generic variable MyGenericStruct`1. Use a supported type or provide a custom reader",
                "WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructItem.SyncDictionaryErrorForGenericStructItem/MyGenericStruct`1<System.Single>");
            HasError("Cannot generate writer for generic type MyGenericStruct`1. Use a supported type or provide a custom writer",
                "WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructItem.SyncDictionaryErrorForGenericStructItem/MyGenericStruct`1<System.Single>");
        }

        [Test]
        public void SyncDictionaryErrorForGenericStructKeyWithCustomDeserializeOnly()
        {
            HasError("Cannot generate reader for generic variable MyGenericStruct`1. Use a supported type or provide a custom reader",
                "WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructKeyWithCustomDeserializeOnly.SyncDictionaryErrorForGenericStructKeyWithCustomDeserializeOnly/MyGenericStruct`1<System.Single>");
            HasError("Cannot generate writer for generic type MyGenericStruct`1. Use a supported type or provide a custom writer",
                "WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructKeyWithCustomDeserializeOnly.SyncDictionaryErrorForGenericStructKeyWithCustomDeserializeOnly/MyGenericStruct`1<System.Single>");
        }

        [Test]
        public void SyncDictionaryErrorForGenericStructItemWithCustomDeserializeOnly()
        {
            HasError("Cannot generate reader for generic variable MyGenericStruct`1. Use a supported type or provide a custom reader",
                "WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructItemWithCustomDeserializeOnly.SyncDictionaryErrorForGenericStructItemWithCustomDeserializeOnly/MyGenericStruct`1<System.Single>");
            HasError("Cannot generate writer for generic type MyGenericStruct`1. Use a supported type or provide a custom writer",
                "WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructItemWithCustomDeserializeOnly.SyncDictionaryErrorForGenericStructItemWithCustomDeserializeOnly/MyGenericStruct`1<System.Single>");
        }

        [Test]
        public void SyncDictionaryErrorForGenericStructKeyWithCustomSerializeOnly()
        {
            HasError("Cannot generate reader for generic variable MyGenericStruct`1. Use a supported type or provide a custom reader",
                "WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructKeyWithCustomSerializeOnly.SyncDictionaryErrorForGenericStructKeyWithCustomSerializeOnly/MyGenericStruct`1<System.Single>");
            HasError("Cannot generate writer for generic type MyGenericStruct`1. Use a supported type or provide a custom writer",
                "WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructKeyWithCustomSerializeOnly.SyncDictionaryErrorForGenericStructKeyWithCustomSerializeOnly/MyGenericStruct`1<System.Single>");
        }

        [Test]
        public void SyncDictionaryErrorForGenericStructItemWithCustomSerializeOnly()
        {
            HasError("Cannot generate reader for generic variable MyGenericStruct`1. Use a supported type or provide a custom reader",
                "WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructItemWithCustomSerializeOnly.SyncDictionaryErrorForGenericStructItemWithCustomSerializeOnly/MyGenericStruct`1<System.Single>");
            HasError("Cannot generate writer for generic type MyGenericStruct`1. Use a supported type or provide a custom writer",
                "WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructItemWithCustomSerializeOnly.SyncDictionaryErrorForGenericStructItemWithCustomSerializeOnly/MyGenericStruct`1<System.Single>");
        }

        [Test]
        public void SyncDictionaryGenericStructKeyWithCustomMethods()
        {
            HasError("Cannot generate reader for generic variable MyGenericStruct`1. Use a supported type or provide a custom reader",
                "WeaverSyncDictionaryTests.SyncDictionaryGenericStructKeyWithCustomMethods.SyncDictionaryGenericStructKeyWithCustomMethods/MyGenericStruct`1<System.Single>");
            HasError("Cannot generate writer for generic type MyGenericStruct`1. Use a supported type or provide a custom writer",
                "WeaverSyncDictionaryTests.SyncDictionaryGenericStructKeyWithCustomMethods.SyncDictionaryGenericStructKeyWithCustomMethods/MyGenericStruct`1<System.Single>");
        }

        [Test]
        public void SyncDictionaryGenericStructItemWithCustomMethods()
        {
            HasError("Cannot generate reader for generic variable MyGenericStruct`1. Use a supported type or provide a custom reader",
                "WeaverSyncDictionaryTests.SyncDictionaryGenericStructItemWithCustomMethods.SyncDictionaryGenericStructItemWithCustomMethods/MyGenericStruct`1<System.Single>");
            HasError("Cannot generate writer for generic type MyGenericStruct`1. Use a supported type or provide a custom writer",
                "WeaverSyncDictionaryTests.SyncDictionaryGenericStructItemWithCustomMethods.SyncDictionaryGenericStructItemWithCustomMethods/MyGenericStruct`1<System.Single>");
        }

        [Test]
        public void SyncDictionaryErrorWhenUsingGenericInNetworkBehaviour()
        {
            IsSuccess();
        }
    }
}
