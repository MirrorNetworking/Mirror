using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncVarHookTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void FindsPrivateHook()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void FindsPublicHook()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void FindsStaticHook()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void FindsHookWithGameObjects()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void FindsHookWithNetworkIdentity()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void FindsHookWithOtherOverloadsInOrder()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void FindsHookWithOtherOverloadsInReverseOrder()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        static string OldNewMethodFormat(string hookName, string ValueType)
        {
            return string.Format("void {0}({1} oldValue, {1} newValue)", hookName, ValueType);
        }

        [Test]
        public void ErrorWhenNoHookFound()
        {
            Assert.That(weaverErrors, Contains.Item($"Could not find hook for 'health', hook name 'onChangeHealth'. " +
                $"Method signature should be {OldNewMethodFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorWhenNoHookFound.ErrorWhenNoHookFound::health)"));
        }

        [Test]
        public void ErrorWhenNoHookWithCorrectParametersFound()
        {
            Assert.That(weaverErrors, Contains.Item($"Could not find hook for 'health', hook name 'onChangeHealth'. " +
                $"Method signature should be {OldNewMethodFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorWhenNoHookWithCorrectParametersFound.ErrorWhenNoHookWithCorrectParametersFound::health)"));
        }

        [Test]
        public void ErrorForWrongTypeOldParameter()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for Parameter in hook for 'health', hook name 'onChangeHealth'. " +
                $"Method signature should be {OldNewMethodFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorForWrongTypeOldParameter.ErrorForWrongTypeOldParameter::health)"));
        }

        [Test]
        public void ErrorForWrongTypeNewParameter()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for Parameter in hook for 'health', hook name 'onChangeHealth'. " +
                $"Method signature should be {OldNewMethodFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorForWrongTypeNewParameter.ErrorForWrongTypeNewParameter::health)"));
        }
    }
}
