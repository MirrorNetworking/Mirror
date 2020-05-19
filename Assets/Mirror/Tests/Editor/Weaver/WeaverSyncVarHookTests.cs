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
        public void AutoDetectsStaticHook()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AutoDetectsHookWithGameObject()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AutoDetectsHookWithNetworkIdentity()
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
            return "Hook method should have one of the following signatures\n" +
                NewMethodFormat(hookName, ValueType) + "\n" +
                OldNewMethodFormat(hookName, ValueType) + "\n" +
                OldNewInitialMethodFormat(hookName, ValueType);
        }

        static string NewMethodFormat(string hookName, string ValueType)
        {
            return string.Format("void {0}({1} newValue)", hookName, ValueType);
        }

        static string OldNewMethodFormat(string hookName, string ValueType)
        {
            return string.Format("void {0}({1} oldValue, {1} newValue)", hookName, ValueType);
        }

        static string OldNewInitialMethodFormat(string hookName, string ValueType)
        {
            return string.Format("void {0}({1} oldValue, {1} newValue, bool initialState)", hookName, ValueType);
        }

        [Test]
        public void ErrorWhenNoHookAutoDetected()
        {
            Assert.That(weaverErrors, Contains.Item($"No hook with correct parameters found for 'health', hook name 'onChangeHealth'. {HookImplementationFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorWhenNoHookAutoDetected.ErrorWhenNoHookAutoDetected::health)"));
        }

        [Test]
        public void ErrorWhenNoHookWithCorrectParametersAutoDetected()
        {
            Assert.That(weaverErrors, Contains.Item($"No hook with correct parameters found for 'health', hook name 'onChangeHealth'. {HookImplementationFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorWhenNoHookWithCorrectParametersAutoDetected.ErrorWhenNoHookWithCorrectParametersAutoDetected::health)"));
        }

        [Test]
        public void ErrorWhenMultipleHooksAutoDetected()
        {
            Assert.That(weaverErrors, Contains.Item($"Multiple hooks found for for 'health', hook name 'onChangeHealth'. " +
                $"Use the hookParameter option in the SyncVar Attribute to pick which one to use. " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorWhenMultipleHooksAutoDetected.ErrorWhenMultipleHooksAutoDetected::health)"));
        }

        [Test]
        public void ErrorWhenExplicitNewHookIsntFound()
        {
            Assert.That(weaverErrors, Contains.Item($"Could not find hook for 'health', hook name 'onChangeHealth' with 'New' parameters. " +
                $"Method signature should be {NewMethodFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorWhenExplicitNewHookIsntFound.ErrorWhenExplicitNewHookIsntFound::health)"));
        }

        [Test]
        public void ErrorWhenExplicitOldNewHookIsntFound()
        {
            Assert.That(weaverErrors, Contains.Item($"Could not find hook for 'health', hook name 'onChangeHealth' with 'OldNew' parameters. " +
                $"Method signature should be {OldNewMethodFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorWhenExplicitOldNewHookIsntFound.ErrorWhenExplicitOldNewHookIsntFound::health)"));
        }

        [Test]
        public void ErrorWhenExplicitOldNewInitialHookIsntFound()
        {
            Assert.That(weaverErrors, Contains.Item($"Could not find hook for 'health', hook name 'onChangeHealth' with 'OldNewInitial' parameters. " +
                $"Method signature should be {OldNewInitialMethodFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorWhenExplicitOldNewInitialHookIsntFound.ErrorWhenExplicitOldNewInitialHookIsntFound::health)"));
        }

        [Test]
        public void ErrorForWrongTypeNewParametersInNew()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for Parameter in hook for 'health', hook name 'onChangeHealth' with 'New' parameters. " +
                $"Method signature should be {NewMethodFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorForWrongTypeNewParametersInNew.ErrorForWrongTypeNewParametersInNew::health)"));
        }

        [Test]
        public void ErrorForWrongTypeOldParametersInOldNew()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for Parameter in hook for 'health', hook name 'onChangeHealth' with 'OldNew' parameters. " +
                $"Method signature should be {OldNewMethodFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorForWrongTypeOldParametersInOldNew.ErrorForWrongTypeOldParametersInOldNew::health)"));
        }

        [Test]
        public void ErrorForWrongTypeNewParametersInOldNew()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for Parameter in hook for 'health', hook name 'onChangeHealth' with 'OldNew' parameters. " +
                $"Method signature should be {OldNewMethodFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorForWrongTypeNewParametersInOldNew.ErrorForWrongTypeNewParametersInOldNew::health)"));
        }

        [Test]
        public void ErrorForWrongTypeOldParametersInOldNewInitial()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for Parameter in hook for 'health', hook name 'onChangeHealth' with 'OldNewInitial' parameters. " +
                $"Method signature should be {OldNewInitialMethodFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorForWrongTypeOldParametersInOldNewInitial.ErrorForWrongTypeOldParametersInOldNewInitial::health)"));
        }

        [Test]
        public void ErrorForWrongTypeNewParametersInOldNewInitial()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for Parameter in hook for 'health', hook name 'onChangeHealth' with 'OldNewInitial' parameters. " +
                $"Method signature should be {OldNewInitialMethodFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorForWrongTypeNewParametersInOldNewInitial.ErrorForWrongTypeNewParametersInOldNewInitial::health)"));
        }

        [Test]
        public void ErrorForWrongTypeInitialParametersInOldNewInitial()
        {
            Assert.That(weaverErrors, Contains.Item($"Wrong type for Parameter in hook for 'health', hook name 'onChangeHealth' with 'OldNewInitial' parameters. " +
                $"Method signature should be {OldNewInitialMethodFormat("onChangeHealth", "System.Int32")} " +
                $"(at System.Int32 WeaverSyncVarHookTests.ErrorForWrongTypeInitialParametersInOldNewInitial.ErrorForWrongTypeInitialParametersInOldNewInitial::health)"));
        }
    }
}
