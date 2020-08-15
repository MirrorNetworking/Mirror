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
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForClass()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForClassInherited()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForClassWithValidConstructor()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void GivesErrorForClassWithNoValidConstructor()
        {
            Assert.That(weaverErrors, Contains.Item("SomeOtherData can't be deserialized because it has no default constructor (at GeneratedReaderWriter.GivesErrorForClassWithNoValidConstructor.SomeOtherData)"));
        }

        [Test]
        public void CreatesForInheritedFromScriptableObject()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForStructFromDifferentAssemblies()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForClassFromDifferentAssemblies()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForClassFromDifferentAssembliesWithValidConstructor()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CanUseCustomReadWriteForTypesFromDifferentAssemblies()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void GivesErrorWhenUsingUnityAsset()
        {
            Assert.That(weaverErrors, Contains.Item("Material can't be deserialized because it has no default constructor (at UnityEngine.Material)"));
        }

        [Test]
        public void GivesErrorWhenUsingObject()
        {
            // TODO: decide if we want to block sending of Object
            // would only want to be send as an arg as a base type for an Inherited object
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for Object. Use a supported type or provide a custom writer (at UnityEngine.Object)"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for Object. Use a supported type or provide a custom reader (at UnityEngine.Object)"));
        }

        [Test]
        public void GivesErrorWhenUsingScriptableObject()
        {
            // TODO: decide if we want to block sending of ScripableObject
            // would only want to be send as an arg as a base type for an Inherited object
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for ScriptableObject. Use a supported type or provide a custom writer (at UnityEngine.ScriptableObject)"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for ScriptableObject. Use a supported type or provide a custom reader (at UnityEngine.ScriptableObject)"));
        }

        [Test]
        public void GivesErrorWhenUsingMonoBehaviour()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type MonoBehaviour. Use a supported type or provide a custom writer (at UnityEngine.MonoBehaviour)"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for component type MonoBehaviour. Use a supported type or provide a custom reader (at UnityEngine.MonoBehaviour)"));
        }

        [Test]
        public void GivesErrorWhenUsingTypeInheritedFromMonoBehaviour()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type MyBehaviour. Use a supported type or provide a custom writer (at GeneratedReaderWriter.GivesErrorWhenUsingTypeInheritedFromMonoBehaviour.MyBehaviour)"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for component type MyBehaviour. Use a supported type or provide a custom reader (at GeneratedReaderWriter.GivesErrorWhenUsingTypeInheritedFromMonoBehaviour.MyBehaviour)"));
        }

        [Test]
        public void ExcludesNonSerializedFields()
        {
            // we test this by having a not allowed type in the class, but mark it with NonSerialized
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void GivesErrorWhenUsingInterface()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for interface IData. Use a supported type or provide a custom writer (at GeneratedReaderWriter.GivesErrorWhenUsingInterface.IData)"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for interface IData. Use a supported type or provide a custom reader (at GeneratedReaderWriter.GivesErrorWhenUsingInterface.IData)"));
        }

        [Test]
        public void CanUseCustomReadWriteForInterfaces()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForEnums()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForArraySegment()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForStructArraySegment()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void GivesErrorForJaggedArray()
        {
            Assert.That(weaverErrors, Contains.Item($"Int32[][] is an unsupported type. Jagged and multidimensional arrays are not supported (at System.Int32[][])"));
        }

        [Test]
        public void GivesErrorForMultidimensionalArray()
        {
            Assert.That(weaverErrors, Contains.Item($"Int32[0...,0...] is an unsupported type. Jagged and multidimensional arrays are not supported (at System.Int32[0...,0...])"));
        }

        [Test]
        public void GivesErrorForInvalidArrayType()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for Array because element MonoBehaviour does not have a writer. Use a supported type or provide a custom writer (at UnityEngine.MonoBehaviour[])"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for Array because element MonoBehaviour does not have a reader. Use a supported type or provide a custom reader (at UnityEngine.MonoBehaviour[])"));
        }

        [Test]
        public void GivesErrorForInvalidArraySegmentType()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for ArraySegment because element MonoBehaviour does not have a writer. Use a supported type or provide a custom writer (at System.ArraySegment`1<UnityEngine.MonoBehaviour>)"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for ArraySegment because element MonoBehaviour does not have a reader. Use a supported type or provide a custom reader (at System.ArraySegment`1<UnityEngine.MonoBehaviour>)"));
        }

        [Test]
        public void CreatesForList()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CreatesForStructList()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void GivesErrorForInvalidListType()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for List because element MonoBehaviour does not have a writer. Use a supported type or provide a custom writer (at System.Collections.Generic.List`1<UnityEngine.MonoBehaviour>)"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for List because element MonoBehaviour does not have a reader. Use a supported type or provide a custom reader (at System.Collections.Generic.List`1<UnityEngine.MonoBehaviour>)"));
        }
    }
}
