using System;
using NUnit.Framework;

namespace Mirror.Tests.Tools
{
    public class Vector4LongTests
    {
        [Test]
        public void Constructor()
        {
            Vector4Long v = new Vector4Long();
            Assert.That(v.x, Is.EqualTo(0));
            Assert.That(v.y, Is.EqualTo(0));
            Assert.That(v.z, Is.EqualTo(0));
            Assert.That(v.w, Is.EqualTo(0));
        }

        [Test]
        public void ConstructorXYZ()
        {
            Vector4Long v = new Vector4Long(1, 2, 3, 4);
            Assert.That(v.x, Is.EqualTo(1));
            Assert.That(v.y, Is.EqualTo(2));
            Assert.That(v.z, Is.EqualTo(3));
            Assert.That(v.w, Is.EqualTo(4));
        }

        [Test]
        public void OperatorAdd()
        {
            Vector4Long a = new Vector4Long(1, 2, 3, 4);
            Vector4Long b = new Vector4Long(2, 3, 4, 5);
            Assert.That(a + b, Is.EqualTo(new Vector4Long(3, 5, 7, 9)));
        }

        [Test]
        public void OperatorSubtract()
        {
            Vector4Long a = new Vector4Long(1, 2, 3, 4);
            Vector4Long b = new Vector4Long(-2, -3, -4, -5);
            Assert.That(a - b, Is.EqualTo(new Vector4Long(3, 5, 7, 9)));
        }

        [Test]
        public void OperatorInverse()
        {
            Vector4Long v = new Vector4Long(1, 2, 3, 4);
            Assert.That(-v, Is.EqualTo(new Vector4Long(-1, -2, -3, -4)));
        }

        [Test]
        public void OperatorMultiply()
        {
            Vector4Long a = new Vector4Long(1, 2, 3, 4);
            // a * n, n * a are two different operators. test both.
            Assert.That(a * 2, Is.EqualTo(new Vector4Long(2, 4, 6, 8)));
            Assert.That(2 * a, Is.EqualTo(new Vector4Long(2, 4, 6, 8)));
        }

#if UNITY_2021_3_OR_NEWER
        [Test]
        public void OperatorEquals()
        {
            // two vectors which are approximately the same
            Vector4Long a = new Vector4Long(1, 2, 3, 4);
            Vector4Long b = new Vector4Long(1, 2, 3, 4);
            Assert.That(a == b, Is.True);

            // two vectors which are definitely not the same
            Assert.That(a == Vector4Long.one, Is.False);
        }

        [Test]
        public void OperatorNotEquals()
        {
            // two vectors which are approximately the same
            Vector4Long a = new Vector4Long(1, 2, 3, 4);
            Vector4Long b = new Vector4Long(1, 2, 3, 4);
            Assert.That(a != b, Is.False);

            // two vectors which are definitely not the same
            Assert.That(a != Vector4Long.one, Is.True);
        }
#endif

        [Test]
        public void OperatorIndexer()
        {
            Vector4Long a = new Vector4Long(1, 2, 3, 4);

            // get
            Assert.That(a[0], Is.EqualTo(1));
            Assert.That(a[1], Is.EqualTo(2));
            Assert.That(a[2], Is.EqualTo(3));
            Assert.That(a[3], Is.EqualTo(4));
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                double _ = a[-1];
            });
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                double _ = a[4];
            });

            // set
            a[0] = -1;
            a[1] = -2;
            a[2] = -3;
            a[3] = -4;
            Assert.Throws<IndexOutOfRangeException>(() => { a[-1] = 0; });
            Assert.Throws<IndexOutOfRangeException>(() => { a[4] = 0; });
            Assert.That(a, Is.EqualTo(new Vector4Long(-1, -2, -3, -4)));
        }

        [Test]
        public void ToStringTest()
        {
            // should be rounded to :F2
            Vector4Long v = new Vector4Long(-10, 0, 42, 3);
            Assert.That(v.ToString(), Is.EqualTo("(-10 0 42 3)"));
        }

#if UNITY_2021_3_OR_NEWER
        [Test]
        public void EqualsVector4Long()
        {
            Assert.That(Vector4Long.one.Equals(Vector4Long.one), Is.True);
            Assert.That(Vector4Long.one.Equals(Vector4Long.zero), Is.False);
        }

        [Test]
        public void EqualsObject()
        {
            Assert.That(Vector4Long.one.Equals((object)42), Is.False);
            Assert.That(Vector4Long.one.Equals((object)Vector4Long.one), Is.True);
            Assert.That(Vector4Long.one.Equals((object)Vector4Long.zero), Is.False);
        }

        [Test]
        public void GetHashCodeTest()
        {
            // shouldn't be 0
            Assert.That(Vector4Long.zero.GetHashCode(), !Is.EqualTo(0));
            Assert.That(Vector4Long.one.GetHashCode(), !Is.EqualTo(0));

            // should be same for same vector
            Assert.That(Vector4Long.zero.GetHashCode(), Is.EqualTo(Vector4Long.zero.GetHashCode()));

            // should be different for different vectors
            Assert.That(Vector4Long.zero.GetHashCode(), !Is.EqualTo(Vector4Long.one.GetHashCode()));
        }
#endif
    }
}
