// base class for networking tests to make things easier.
using System.Collections;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public abstract class MirrorPlayModeTest : MirrorTest
    {
        // when overwriting, call it like this:
        //   yield return base.UnitySetUp();
        [UnitySetUp]
        public virtual IEnumerator UnitySetUp()
        {
            base.SetUp();
            yield return null;
        }

        [UnityTearDown]
        public virtual IEnumerator UnityTearDown()
        {
            base.TearDown();
            yield return null;
        }
    }
}
