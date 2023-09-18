// base class for networking tests to make things easier.
using System.Collections;
using GodotEngine.TestTools;

namespace Mirror.Tests
{
    public abstract class MirrorPlayModeTest : MirrorTest
    {
        // when overwriting, call it like this:
        //   yield return base.GodotSetUp();
        [GodotSetUp]
        public virtual IEnumerator GodotSetUp()
        {
            base.SetUp();
            yield return null;
        }

        // when overwriting, call it like this:
        //   yield return base.GodotTearDown();
        [GodotTearDown]
        public virtual IEnumerator GodotTearDown()
        {
            base.TearDown();
            yield return null;
        }
    }
}
