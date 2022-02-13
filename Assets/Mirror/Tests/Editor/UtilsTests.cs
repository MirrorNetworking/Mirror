using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class UtilsTests
    {
        [Test]
        public void GetTrueRandomUInt()
        {
            uint first = Utils.GetTrueRandomUInt();
            uint second = Utils.GetTrueRandomUInt();
            Assert.That(first, !Is.EqualTo(second));
        }

        [Test]
        public void IsPointInScreen()
        {
            int width = Screen.width;
            int height = Screen.height;
            Assert.That(Utils.IsPointInScreen(new Vector2(-1, -1)), Is.False);

            Assert.That(Utils.IsPointInScreen(new Vector2(0, 0)), Is.True);
            Assert.That(Utils.IsPointInScreen(new Vector2(width / 2, height / 2)), Is.True);

            Assert.That(Utils.IsPointInScreen(new Vector2(width, height / 2)), Is.False);
            Assert.That(Utils.IsPointInScreen(new Vector2(width / 2, height)), Is.False);
            Assert.That(Utils.IsPointInScreen(new Vector2(width + 1, height + 1)), Is.False);
        }
    }
}
