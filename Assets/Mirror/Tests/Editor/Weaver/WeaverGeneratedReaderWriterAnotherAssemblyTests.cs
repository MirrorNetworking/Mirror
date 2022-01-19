using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverGeneratedReaderWriterAnotherAssemblyTests : WeaverTestsBuildFromTestName
    {
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
