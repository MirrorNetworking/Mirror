using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncVarHookTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void AutoDetectsPrivateHook()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AutoDetectsNewHook()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AutoDetectsOldNewHook()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AutoDetectsOldNewInitialHook()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AutoDetectsWithOtherOverloads()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AutoDetectsWithOtherOverloadsReverseOrder()
        {
            Assert.That(weaverErrors, Is.Empty);
        }


        [Test]
        public void FindsExplicitNewHookWithOtherOverloads()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void FindsExplicitOldNewHookWithOtherOverloads()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void FindsExplicitOldNewInitialHookWithOtherOverloads()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        static string HookImplementationFormat(string hookName, string ValueType)
        {
            return "Hook method should have one of the following overloads\n" +
                NewMethodFormat(hookName, ValueType) +
                OldNewMethodFormat(hookName, ValueType) +
                OldNewInitalMethodFormat(hookName, ValueType);
        }

        static string NewMethodFormat(string hookName, string ValueType)
        {
            return string.Format("void {0}({1} newValue)\n", hookName, ValueType);
        }

        static string OldNewMethodFormat(string hookName, string ValueType)
        {
            return string.Format("void {0}({1} oldValue, {1} newValue)\n", hookName, ValueType);
        }

        static string OldNewInitalMethodFormat(string hookName, string ValueType)
        {
            return string.Format("void {0}({1} oldValue, {1} newValue, bool initialState)", hookName, ValueType);
        }

        [Test]
        public void ErrorWhenNoHookAutoDetected()
        {
            Assert.That(weaverErrors, Contains.Item($"No hook with correct parameters found for 'health', hook name 'onChangeHealth'. {HookImplementationFormat("onChangeHealth", " System.Int32")}"));
        }

        [Test]
        public void ErrorWhenNoHookWithCorrectParametersAutoDetected()
        {
            Assert.That(weaverErrors, Contains.Item($"No hook with correct parameters found for 'health', hook name 'onChangeHealth'. {HookImplementationFormat("onChangeHealth", " System.Int32")}"));
        }

        [Test]
        public void ErrorWhenMultipleHooksAutoDetected()
        {
            Assert.That(weaverErrors, Contains.Item($"Multiple hooks found for for 'health', hook name 'onChangeHealth'. Use the hookParameter option in the SyncVar Attribute to pick which one to use."));
        }

        [Test]
        public void ErrorWhenExplicitNewHookIsntFound()
        {
            Assert.That(weaverErrors, Contains.Item($"Could not find hook for 'health', hook name 'onChangeHealth' with 'New' parameters. Method signature should be {NewMethodFormat("onChangeHealth", " System.Int32")}"));
        }

        [Test]
        public void ErrorWhenExplicitOldNewHookIsntFound()
        {
            Assert.That(weaverErrors, Contains.Item($"Could not find hook for 'health', hook name 'onChangeHealth' with 'OldNew' parameters. Method signature should be {OldNewMethodFormat("onChangeHealth", " System.Int32")}"));
        }

        [Test]
        public void ErrorWhenExplicitOldNewInitialHookIsntFound()
        {
            Assert.That(weaverErrors, Contains.Item($"Could not find hook for 'health', hook name 'onChangeHealth' with 'OldNewInital' parameters. Method signature should be {OldNewInitalMethodFormat("onChangeHealth", " System.Int32")}"));
        }

        [Test]
        public void ErrorForWrongTypeNewParametersInNew()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for 'wrongNewValue' parameter in hook for 'health', hook name 'onChangeHealth' with 'New' parameters. Method signature should be {NewMethodFormat("onChangeHealth", " System.Int32")}"));
        }

        [Test]
        public void ErrorForWrongTypeOldParametersInOldNew()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for 'wrongOldValue' parameter in hook for 'health', hook name 'onChangeHealth' with 'New' parameters. Method signature should be {OldNewMethodFormat("onChangeHealth", " System.Int32")}"));
        }

        [Test]
        public void ErrorForWrongTypeNewParametersInOldNew()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for 'wrongNewValue' parameter in hook for 'health', hook name 'onChangeHealth' with 'New' parameters. Method signature should be {OldNewMethodFormat("onChangeHealth", " System.Int32")}"));
        }

        [Test]
        public void ErrorForWrongTypeOldParametersInOldNewInital()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for 'wrongOldValue' parameter in hook for 'health', hook name 'onChangeHealth' with 'New' parameters. Method signature should be {OldNewInitalMethodFormat("onChangeHealth", " System.Int32")}"));
        }

        [Test]
        public void ErrorForWrongTypeNewParametersInOldNewInital()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for 'wrongNewValue' parameter in hook for 'health', hook name 'onChangeHealth' with 'New' parameters. Method signature should be {OldNewInitalMethodFormat("onChangeHealth", " System.Int32")}"));
        }

        [Test]
        public void ErrorForWrongTypeInitalParametersInOldNewInital()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for 'wrongInitialState' parameter in hook for 'health', hook name 'onChangeHealth' with 'New' parameters. Method signature should be {OldNewInitalMethodFormat("onChangeHealth", " System.Int32")}"));
        }
    }
}
