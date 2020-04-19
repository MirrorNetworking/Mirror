using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverGeneratedReaderWriterTests : WeaverTests
    {
        protected void BuildAndWeaveTestAssembly(string testScript)
        {
            const string folderName = "GeneratedReaderWriter";
            BuildAndWeaveTestAssembly(folderName, testScript);
        }

        [SetUp]
        public void TestSetup()
        {
            WeaverAssembler.AddReferencesByAssemblyName(new string[] { "WeaverTestExtraAssembly.dll" });

            BuildAndWeaveTestAssembly(TestContext.CurrentContext.Test.Name);
        }

        [Test]
        public void CreatesForStructs()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForClass()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForClassWithValidConstructor()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void GivesErrorForClassWithNoValidConstructor()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Has.Some.Match(@"Mirror\.Weaver error: MirrorTest\.SomeOtherData can't be deserialized because it has no default constructor"));
        }

        [Test]
        public void CreatesForInheritedFromScriptableObject()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForStructFromDifferentAssemblies()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForClassFromDifferentAssemblies()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForClassFromDifferentAssembliesWithValidConstructor()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CanUseCustomReadWriteForTypesFromDifferentAssemblies()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void GivesErrorWhenUsingUnityAsset()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Has.Some.Match(@"Mirror\.Weaver error: UnityEngine\.Material can't be deserialized because it has no default constructor"));
        }

        [Test]
        public void GivesErrorWhenUsingObject()
        {
            // TODO: decide if we want to block sending of Object
            // would only want to be send as an arg as a base type for an Inherited object
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Has.Some.Match(@"Cannot generate writer for UnityEngine\.Object\. Use a supported type or provide a custom writer"));
            Assert.That(weaverErrors, Has.Some.Match(@"Cannot generate reader for UnityEngine\.Object\. Use a supported type or provide a custom reader"));
        }

        [Test]
        public void GivesErrorWhenUsingScriptableObject()
        {
            // TODO: decide if we want to block sending of ScripableObject
            // would only want to be send as an arg as a base type for an Inherited object
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Has.Some.Match(@"Cannot generate writer for UnityEngine\.ScriptableObject\. Use a supported type or provide a custom writer"));
            Assert.That(weaverErrors, Has.Some.Match(@"Cannot generate reader for UnityEngine\.ScriptableObject\. Use a supported type or provide a custom reader"));
        }

        [Test]
        public void GivesErrorWhenUsingMonoBehaviour()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Has.Some.Match(@"Cannot generate writer for component type UnityEngine\.MonoBehaviour\. Use a supported type or provide a custom writer"));
            Assert.That(weaverErrors, Has.Some.Match(@"Cannot generate reader for component type UnityEngine\.MonoBehaviour\. Use a supported type or provide a custom reader"));
        }

        [Test]
        public void GivesErrorWhenUsingTypeInheritedFromMonoBehaviour()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Has.Some.Match(@"Cannot generate writer for component type MirrorTest\.MyBehaviour\. Use a supported type or provide a custom writer"));
            Assert.That(weaverErrors, Has.Some.Match(@"Cannot generate reader for component type MirrorTest\.MyBehaviour\. Use a supported type or provide a custom reader"));
        }

        [Test]
        [Ignore("Not Implemented")]
        public void ExcludesNonSerializedFields()
        {
            // we test this by having a not allowed type in the class, but mark it with NonSerialized
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void GivesErrorWhenUsingInterface()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Has.Some.Match(@"Cannot generate writer for interface MirrorTest\.IData\. Use a supported type or provide a custom writer"));
            Assert.That(weaverErrors, Has.Some.Match(@"Cannot generate reader for interface MirrorTest\.IData\. Use a supported type or provide a custom reader"));
        }

        [Test]
        public void CanUseCustomReadWriteForInterfaces()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }
    }
}
