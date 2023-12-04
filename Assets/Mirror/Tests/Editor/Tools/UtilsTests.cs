using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.Tools
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

        [Test]
        public void PrettyBytes()
        {
            // bytes
            Assert.That(Utils.PrettyBytes(0), Is.EqualTo("0 B"));
            Assert.That(Utils.PrettyBytes(512), Is.EqualTo("512 B"));
            Assert.That(Utils.PrettyBytes(1023), Is.EqualTo("1023 B"));

            // kilobytes
            Assert.That(Utils.PrettyBytes(1024), Is.EqualTo($"{1.0:F2} KB"));
            Assert.That(Utils.PrettyBytes(1024 + 512), Is.EqualTo($"{1.5:F2} KB"));
            Assert.That(Utils.PrettyBytes(2048), Is.EqualTo($"{2.0:F2} KB"));

            // megabytes
            Assert.That(Utils.PrettyBytes(1024 * 1024), Is.EqualTo($"{1.0:F2} MB"));
            Assert.That(Utils.PrettyBytes(1024 * (1024 + 512)), Is.EqualTo($"{1.5:F2} MB"));
            Assert.That(Utils.PrettyBytes(1024 * 1024 * 2), Is.EqualTo($"{2.0:F2} MB"));

            // gigabytes
            Assert.That(Utils.PrettyBytes(1024L * 1024L * 1024L), Is.EqualTo($"{1.0:F2} GB"));
            Assert.That(Utils.PrettyBytes(1024L * 1024L * (1024L + 512L)), Is.EqualTo($"{1.5:F2} GB"));
            Assert.That(Utils.PrettyBytes(1024L * 1024L * 1024L * 2L), Is.EqualTo($"{2.0:F2} GB"));
        }

        [Test]
        public void PrettySeconds()
        {
            Assert.That(Utils.PrettySeconds(0), Is.EqualTo("0s"));
            Assert.That(Utils.PrettySeconds(0.5f), Is.EqualTo("0.5s"));
            Assert.That(Utils.PrettySeconds(1), Is.EqualTo("1s"));
            Assert.That(Utils.PrettySeconds(1.5f), Is.EqualTo("1.5s"));
            Assert.That(Utils.PrettySeconds(65), Is.EqualTo("1m 5s"));
            Assert.That(Utils.PrettySeconds(60 * 61 + 5), Is.EqualTo("1h 1m 5s"));
            Assert.That(Utils.PrettySeconds(24 * 60 * 60 + 60 * 61 + 5), Is.EqualTo("1d 1h 1m 5s"));
        }
    }
}
