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
            Assert.That(weaverErrors, Contains.Item("SomeOtherData can't be deserialized because it has no default constructor (at MirrorTest.SomeOtherData)"));
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
            Assert.That(weaverErrors, Contains.Item("Material can't be deserialized because it has no default constructor (at UnityEngine.Material)"));
        }

        [Test]
        public void GivesErrorWhenUsingObject()
        {
            // TODO: decide if we want to block sending of Object
            // would only want to be send as an arg as a base type for an Inherited object
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for Object. Use a supported type or provide a custom writer (at UnityEngine.Object)"));
            Assert.That(weaverErrors, Contains.Item("RpcDoSomething has invalid parameter obj (at System.Void MirrorTest.GivesErrorWhenUsingObject::RpcDoSomething(UnityEngine.Object))"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for Object. Use a supported type or provide a custom reader (at UnityEngine.Object)"));
            Assert.That(weaverErrors, Contains.Item("RpcDoSomething has invalid parameter obj.  Unsupported type UnityEngine.Object,  use a supported Mirror type instead (at System.Void MirrorTest.GivesErrorWhenUsingObject::RpcDoSomething(UnityEngine.Object))"));
        }

        [Test]
        public void GivesErrorWhenUsingScriptableObject()
        {
            // TODO: decide if we want to block sending of ScripableObject
            // would only want to be send as an arg as a base type for an Inherited object
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for ScriptableObject. Use a supported type or provide a custom writer (at UnityEngine.ScriptableObject)"));
            Assert.That(weaverErrors, Contains.Item("RpcDoSomething has invalid parameter obj (at System.Void MirrorTest.GivesErrorWhenUsingScriptableObject::RpcDoSomething(UnityEngine.ScriptableObject))"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for ScriptableObject. Use a supported type or provide a custom reader (at UnityEngine.ScriptableObject)"));
            Assert.That(weaverErrors, Contains.Item("RpcDoSomething has invalid parameter obj.  Unsupported type UnityEngine.ScriptableObject,  use a supported Mirror type instead (at System.Void MirrorTest.GivesErrorWhenUsingScriptableObject::RpcDoSomething(UnityEngine.ScriptableObject))"));
        }

        [Test]
        public void GivesErrorWhenUsingMonoBehaviour()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type MonoBehaviour. Use a supported type or provide a custom writer (at UnityEngine.MonoBehaviour)"));
            Assert.That(weaverErrors, Contains.Item("RpcDoSomething has invalid parameter behaviour (at System.Void MirrorTest.GivesErrorWhenUsingMonoBehaviour::RpcDoSomething(UnityEngine.MonoBehaviour))"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for component type MonoBehaviour. Use a supported type or provide a custom reader (at UnityEngine.MonoBehaviour)"));
            Assert.That(weaverErrors, Contains.Item("RpcDoSomething has invalid parameter behaviour.  Unsupported type UnityEngine.MonoBehaviour,  use a supported Mirror type instead (at System.Void MirrorTest.GivesErrorWhenUsingMonoBehaviour::RpcDoSomething(UnityEngine.MonoBehaviour))"));
        }

        [Test]
        public void GivesErrorWhenUsingTypeInheritedFromMonoBehaviour()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type MyBehaviour. Use a supported type or provide a custom writer (at MirrorTest.MyBehaviour)"));
            Assert.That(weaverErrors, Contains.Item("RpcDoSomething has invalid parameter behaviour (at System.Void MirrorTest.GivesErrorWhenUsingTypeInheritedFromMonoBehaviour::RpcDoSomething(MirrorTest.MyBehaviour))"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for component type MyBehaviour. Use a supported type or provide a custom reader (at MirrorTest.MyBehaviour)"));
            Assert.That(weaverErrors, Contains.Item("RpcDoSomething has invalid parameter behaviour.  Unsupported type MirrorTest.MyBehaviour,  use a supported Mirror type instead (at System.Void MirrorTest.GivesErrorWhenUsingTypeInheritedFromMonoBehaviour::RpcDoSomething(MirrorTest.MyBehaviour))"));
        }

        [Test]
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
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for interface IData. Use a supported type or provide a custom writer (at MirrorTest.IData)"));
            Assert.That(weaverErrors, Contains.Item("RpcDoSomething has invalid parameter data (at System.Void MirrorTest.GivesErrorWhenUsingInterface::RpcDoSomething(MirrorTest.IData))"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for interface IData. Use a supported type or provide a custom reader (at MirrorTest.IData)"));
            Assert.That(weaverErrors, Contains.Item("RpcDoSomething has invalid parameter data.  Unsupported type MirrorTest.IData,  use a supported Mirror type instead (at System.Void MirrorTest.GivesErrorWhenUsingInterface::RpcDoSomething(MirrorTest.IData))"));
        }

        [Test]
        public void CanUseCustomReadWriteForInterfaces()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForEnums()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }
    }
}
