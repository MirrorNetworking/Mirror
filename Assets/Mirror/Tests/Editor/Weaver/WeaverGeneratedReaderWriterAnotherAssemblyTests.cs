using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverGeneratedReaderWriterAnotherAssemblyTests : WeaverTestsBuildFromTestName
    {
        [SetUp]
        public override void TestSetup()
        {
            WeaverAssembler.AddReferencesByAssemblyName(new string[] { "WeaverTestExtraAssembly.dll" });

            base.TestSetup();
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
        public void CreatesForComplexTypeFromDifferentAssemblies()
        {
            IsSuccess();
        }

        [Test]
        public void CreatesForTypeThatUsesDifferentAssemblies()
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
    }
}
