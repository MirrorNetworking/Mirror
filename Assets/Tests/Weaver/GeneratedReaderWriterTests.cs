using NUnit.Framework;

namespace Mirror.Weaver
{
    public class GeneratedReaderWriterTests : TestsBuildFromTestName
    {
        [SetUp]
        public override void TestSetup()
        {
            base.TestSetup();
        }

        [Test]
        public void CreatesForStructs()
        {
            IsSuccess();
        }

        [Test]
        public void CreateForExplicitNetworkMessage()
        {
            IsSuccess();
        }

        [Test]
        public void CreatesForClass()
        {
            IsSuccess();
        }

        [Test]
        public void CreatesForClassInherited()
        {
            IsSuccess();
        }

        [Test]
        public void CreatesForClassWithValidConstructor()
        {
            IsSuccess();
        }

        [Test]
        public void GivesErrorForClassWithNoValidConstructor()
        {
            HasError("SomeOtherData can't be deserialized because it has no default constructor",
                "GeneratedReaderWriter.GivesErrorForClassWithNoValidConstructor.SomeOtherData");
        }

        [Test]
        public void CreatesForInheritedFromScriptableObject()
        {
            IsSuccess();
        }

        [Test]
        public void CreatesForStructFromDifferentAssemblies()
        {
            IsSuccess();
        }

        [Test]
        public void CreatesForClassFromDifferentAssemblies()
        {
            IsSuccess();
        }

        [Test]
        public void CreatesForClassFromDifferentAssembliesWithValidConstructor()
        {
            IsSuccess();
        }

        [Test]
        public void CanUseCustomReadWriteForTypesFromDifferentAssemblies()
        {
            IsSuccess();
        }

        [Test]
        public void GivesErrorWhenUsingUnityAsset()
        {
            HasError("Material can't be deserialized because it has no default constructor",
                "UnityEngine.Material");
        }

        [Test]
        public void GivesErrorWhenUsingObject()
        {
            HasError("Cannot generate writer for Object. Use a supported type or provide a custom writer",
                "UnityEngine.Object");
            HasError("Cannot generate reader for Object. Use a supported type or provide a custom reader",
                "UnityEngine.Object");
        }

        [Test]
        public void GivesErrorWhenUsingScriptableObject()
        {
            HasError("Cannot generate writer for ScriptableObject. Use a supported type or provide a custom writer",
                "UnityEngine.ScriptableObject");
            HasError("Cannot generate reader for ScriptableObject. Use a supported type or provide a custom reader",
                "UnityEngine.ScriptableObject");
        }

        [Test]
        public void GivesErrorWhenUsingMonoBehaviour()
        {
            HasError("Cannot generate writer for component type MonoBehaviour. Use a supported type or provide a custom writer",
                "UnityEngine.MonoBehaviour");
            HasError("Cannot generate reader for component type MonoBehaviour. Use a supported type or provide a custom reader",
                "UnityEngine.MonoBehaviour");
        }

        [Test]
        public void GivesErrorWhenUsingTypeInheritedFromMonoBehaviour()
        {
            HasError("Cannot generate writer for component type MyBehaviour. Use a supported type or provide a custom writer",
                "GeneratedReaderWriter.GivesErrorWhenUsingTypeInheritedFromMonoBehaviour.MyBehaviour");
            HasError("Cannot generate reader for component type MyBehaviour. Use a supported type or provide a custom reader",
                "GeneratedReaderWriter.GivesErrorWhenUsingTypeInheritedFromMonoBehaviour.MyBehaviour");
        }

        [Test]
        public void ExcludesNonSerializedFields()
        {
            // we test this by having a not allowed type in the class, but mark it with NonSerialized
            IsSuccess();
        }

        [Test]
        public void GivesErrorWhenUsingInterface()
        {
            HasError("Cannot generate writer for interface IData. Use a supported type or provide a custom writer",
                "GeneratedReaderWriter.GivesErrorWhenUsingInterface.IData");
            HasError("Cannot generate reader for interface IData. Use a supported type or provide a custom reader",
                "GeneratedReaderWriter.GivesErrorWhenUsingInterface.IData");
        }

        [Test]
        public void CanUseCustomReadWriteForInterfaces()
        {
            IsSuccess();
        }

        [Test]
        public void GivesErrorWhenUsingAbstractClass()
        {
            HasError("Cannot generate writer for abstract class DataBase. Use a supported type or provide a custom writer",
                "GeneratedReaderWriter.GivesErrorWhenUsingAbstractClass.DataBase");
            HasError("Cannot generate reader for abstract class DataBase. Use a supported type or provide a custom reader",
                "GeneratedReaderWriter.GivesErrorWhenUsingAbstractClass.DataBase");
        }

        [Test]
        public void CanUseCustomReadWriteForAbstractClass()
        {
            IsSuccess();
        }

        [Test]
        public void CreatesForEnums()
        {
            IsSuccess();
        }

        [Test]
        public void CreatesForArraySegment()
        {
            IsSuccess();
        }

        [Test]
        public void CreatesForStructArraySegment()
        {
            IsSuccess();
        }

        [Test]
        public void GivesErrorForJaggedArray()
        {
            IsSuccess();
        }

        [Test]
        public void GivesErrorForMultidimensionalArray()
        {
            HasError("Int32[0...,0...] is an unsupported type. Multidimensional arrays are not supported",
                "System.Int32[0...,0...]");
        }

        [Test]
        public void GivesErrorForInvalidArrayType()
        {
            HasError("Cannot generate writer for UnityEngine.MonoBehaviour[]. Use a supported type or provide a custom writer",
                "UnityEngine.MonoBehaviour[]");
            HasError("Cannot generate writer for component type MonoBehaviour. Use a supported type or provide a custom writer", "UnityEngine.MonoBehaviour");
            HasError("Cannot generate writer for UnityEngine.MonoBehaviour[]. Use a supported type or provide a custom writer", "UnityEngine.MonoBehaviour[]");
            HasError("Cannot generate reader for component type MonoBehaviour. Use a supported type or provide a custom reader", "UnityEngine.MonoBehaviour");

        }

        [Test]
        public void GivesErrorForInvalidArraySegmentType()
        {
            HasError("Cannot generate writer for System.ArraySegment`1<UnityEngine.MonoBehaviour>. Use a supported type or provide a custom writer",
                "System.ArraySegment`1<UnityEngine.MonoBehaviour>");
            HasError("Cannot generate writer for component type MonoBehaviour. Use a supported type or provide a custom writer", "UnityEngine.MonoBehaviour");
            HasError("Cannot generate reader for component type MonoBehaviour. Use a supported type or provide a custom reader", "UnityEngine.MonoBehaviour");
        }

        [Test]
        public void CreatesForList()
        {
            IsSuccess();
        }

        [Test]
        public void CreatesForStructList()
        {
            IsSuccess();
        }

        [Test]
        public void GivesErrorForInvalidListType()
        {
            HasError("Cannot generate writer for System.Collections.Generic.List`1<UnityEngine.MonoBehaviour>. Use a supported type or provide a custom writer",
                "System.Collections.Generic.List`1<UnityEngine.MonoBehaviour>");
            HasError("Cannot generate writer for component type MonoBehaviour. Use a supported type or provide a custom writer", "UnityEngine.MonoBehaviour");
            HasError("Cannot generate writer for System.Collections.Generic.List`1<UnityEngine.MonoBehaviour>. Use a supported type or provide a custom writer", "System.Collections.Generic.List`1<UnityEngine.MonoBehaviour>");
            HasError("Cannot generate reader for component type MonoBehaviour. Use a supported type or provide a custom reader", "UnityEngine.MonoBehaviour");
        }
    }
}
