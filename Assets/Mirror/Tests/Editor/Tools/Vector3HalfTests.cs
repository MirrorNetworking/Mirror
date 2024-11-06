using System;
using NUnit.Framework;

namespace Mirror.Tests.Tools
{
    public class Vector3HalfTests
    {
        [Test]
        public void Constructor()
        {
            Vector3Half v = new Vector3Half();
            Assert.That(v.x, Is.EqualTo((Half)0));
            Assert.That(v.y, Is.EqualTo((Half)0));
            Assert.That(v.z, Is.EqualTo((Half)0));
        }

        [Test]
        public void ConstructorXYZ()
        {
            Vector3Half v = new Vector3Half((Half)1, (Half)2, (Half)3);
            Assert.That(v.x, Is.EqualTo((Half)1));
            Assert.That(v.y, Is.EqualTo((Half)2));
            Assert.That(v.z, Is.EqualTo((Half)3));
        }

        [Test]
        public void OperatorAdd()
        {
            Vector3Half a = new Vector3Half((Half)1, (Half)2, (Half)3);
            Vector3Half b = new Vector3Half((Half)2, (Half)3, (Half)4);
            Assert.That(a + b, Is.EqualTo(new Vector3Half((Half)3, (Half)5, (Half)7)));
        }

        [Test]
        public void OperatorSubtract()
        {
            Vector3Half a = new Vector3Half((Half)1, (Half)2, (Half)3);
            Vector3Half b = new Vector3Half((Half)(-2), (Half)(-3), (Half)(-4));
            Assert.That(a - b, Is.EqualTo(new Vector3Half((Half)3, (Half)5, (Half)7)));
        }

        [Test]
        public void OperatorInverse()
        {
            Vector3Half v = new Vector3Half((Half)1, (Half)2, (Half)3);
            Assert.That(-v, Is.EqualTo(new Vector3Half((Half)(-1), (Half)(-2), (Half)(-3))));
        }

        // [Test]
        // public void OperatorMultiply()
        // {
        //     Vector3Half a = new Vector3Half((Half)1, (Half)2, (Half)3);
        //     // a * n, n * a are two different operators. test both.
        //     Assert.That(a * (Half)2, Is.EqualTo(new Vector3Half((Half)2, (Half)4, (Half)6)));
        //     Assert.That((Half)2 * a, Is.EqualTo(new Vector3Half((Half)2, (Half)4, (Half)6)));
        // }

#if UNITY_2021_3_OR_NEWER
        [Test]
        public void OperatorEquals()
        {
            // two vectors which are approximately the same
            Vector3Half a = new Vector3Half((Half)1, (Half)2, (Half)3);
            Vector3Half b = new Vector3Half((Half)1, (Half)2, (Half)3);
            Assert.That(a == b, Is.True);

            // two vectors which are definitely not the same
            Assert.That(a == Vector3Half.one, Is.False);
        }

        [Test]
        public void OperatorNotEquals()
        {
            // two vectors which are approximately the same
            Vector3Half a = new Vector3Half((Half)1, (Half)2, (Half)3);
            Vector3Half b = new Vector3Half((Half)1, (Half)2, (Half)3);
            Assert.That(a != b, Is.False);

            // two vectors which are definitely not the same
            Assert.That(a != Vector3Half.one, Is.True);
        }
#endif

        [Test]
        public void OperatorIndexer()
        {
            Vector3Half a = new Vector3Half((Half)1, (Half)2, (Half)3);

            // get
            Assert.That(a[0], Is.EqualTo((Half)1));
            Assert.That(a[1], Is.EqualTo((Half)2));
            Assert.That(a[2], Is.EqualTo((Half)3));
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                Half _ = a[-1];
            });
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                Half _ = a[3];
            });

            // set
            a[0] = -1;
            a[1] = -2;
            a[2] = -3;
            Assert.Throws<IndexOutOfRangeException>(() => { a[-1] = (Half)0; });
            Assert.Throws<IndexOutOfRangeException>(() => { a[3] = (Half)0; });
            Assert.That(a, Is.EqualTo(new Vector3Half((Half)(-1), (Half)(-2), (Half)(-3))));
        }

        [Test]
        public void ToStringTest()
        {
            // should be rounded to :F2
            Vector3Half v = new Vector3Half((Half)(-10), (Half)0, (Half)42);
            Assert.That(v.ToString(), Is.EqualTo("(-10 0 42)"));
        }

#if UNITY_2021_3_OR_NEWER
        [Test]
        public void EqualsVector3Half()
        {
            Assert.That(Vector3Half.one.Equals(Vector3Half.one), Is.True);
            Assert.That(Vector3Half.one.Equals(Vector3Half.zero), Is.False);
        }

        [Test]
        public void EqualsObject()
        {
            Assert.That(Vector3Half.one.Equals((object)42), Is.False);
            Assert.That(Vector3Half.one.Equals((object)Vector3Half.one), Is.True);
            Assert.That(Vector3Half.one.Equals((object)Vector3Half.zero), Is.False);
        }

        [Test]
        public void GetHashCodeTest()
        {
            // shouldn't be 0
            Assert.That(Vector3Half.zero.GetHashCode(), !Is.EqualTo(0));
            Assert.That(Vector3Half.one.GetHashCode(), !Is.EqualTo(0));

            // should be same for same vector
            Assert.That(Vector3Half.zero.GetHashCode(), Is.EqualTo(Vector3Half.zero.GetHashCode()));

            // should be different for different vectors
            Assert.That(Vector3Half.zero.GetHashCode(), !Is.EqualTo(Vector3Half.one.GetHashCode()));
        }
#endif
    }
}
