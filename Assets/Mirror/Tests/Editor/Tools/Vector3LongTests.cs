using System;
using NUnit.Framework;

namespace Mirror.Tests.Tools
{
    public class Vector3LongTests
    {
        [Test]
        public void Constructor()
        {
            Vector3Long v = new Vector3Long();
            Assert.That(v.x, Is.EqualTo(0));
            Assert.That(v.y, Is.EqualTo(0));
            Assert.That(v.z, Is.EqualTo(0));
        }

        [Test]
        public void ConstructorXYZ()
        {
            Vector3Long v = new Vector3Long(1, 2, 3);
            Assert.That(v.x, Is.EqualTo(1));
            Assert.That(v.y, Is.EqualTo(2));
            Assert.That(v.z, Is.EqualTo(3));
        }

        [Test]
        public void OperatorAdd()
        {
            Vector3Long a = new Vector3Long(1, 2, 3);
            Vector3Long b = new Vector3Long(2, 3, 4);
            Assert.That(a + b, Is.EqualTo(new Vector3Long(3, 5, 7)));
        }

        [Test]
        public void OperatorSubtract()
        {
            Vector3Long a = new Vector3Long(1, 2, 3);
            Vector3Long b = new Vector3Long(-2, -3, -4);
            Assert.That(a - b, Is.EqualTo(new Vector3Long(3, 5, 7)));
        }

        [Test]
        public void OperatorInverse()
        {
            Vector3Long v = new Vector3Long(1, 2, 3);
            Assert.That(-v, Is.EqualTo(new Vector3Long(-1, -2, -3)));
        }

        [Test]
        public void OperatorMultiply()
        {
            Vector3Long a = new Vector3Long(1, 2, 3);
            // a * n, n * a are two different operators. test both.
            Assert.That(a * 2, Is.EqualTo(new Vector3Long(2, 4, 6)));
            Assert.That(2 * a, Is.EqualTo(new Vector3Long(2, 4, 6)));
        }

#if UNITY_2021_3_OR_NEWER
        [Test]
        public void OperatorEquals()
        {
            // two vectors which are approximately the same
            Vector3Long a = new Vector3Long(1, 2, 3);
            Vector3Long b = new Vector3Long(1, 2, 3);
            Assert.That(a == b, Is.True);

            // two vectors which are definitely not the same
            Assert.That(a == Vector3Long.one, Is.False);
        }

        [Test]
        public void OperatorNotEquals()
        {
            // two vectors which are approximately the same
            Vector3Long a = new Vector3Long(1, 2, 3);
            Vector3Long b = new Vector3Long(1, 2, 3);
            Assert.That(a != b, Is.False);

            // two vectors which are definitely not the same
            Assert.That(a != Vector3Long.one, Is.True);
        }
#endif

        [Test]
        public void OperatorIndexer()
        {
            Vector3Long a = new Vector3Long(1, 2, 3);

            // get
            Assert.That(a[0], Is.EqualTo(1));
            Assert.That(a[1], Is.EqualTo(2));
            Assert.That(a[2], Is.EqualTo(3));
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                double _ = a[-1];
            });
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                double _ = a[3];
            });

            // set
            a[0] = -1;
            a[1] = -2;
            a[2] = -3;
            Assert.Throws<IndexOutOfRangeException>(() => { a[-1] = 0; });
            Assert.Throws<IndexOutOfRangeException>(() => { a[3] = 0; });
            Assert.That(a, Is.EqualTo(new Vector3Long(-1, -2, -3)));
        }

        [Test]
        public void ToStringTest()
        {
            // should be rounded to :F2
            Vector3Long v = new Vector3Long(-10, 0, 42);
            Assert.That(v.ToString(), Is.EqualTo("(-10 0 42)"));
        }

#if UNITY_2021_3_OR_NEWER
        [Test]
        public void EqualsVector3Long()
        {
            Assert.That(Vector3Long.one.Equals(Vector3Long.one), Is.True);
            Assert.That(Vector3Long.one.Equals(Vector3Long.zero), Is.False);
        }

        [Test]
        public void EqualsObject()
        {
            Assert.That(Vector3Long.one.Equals((object)42), Is.False);
            Assert.That(Vector3Long.one.Equals((object)Vector3Long.one), Is.True);
            Assert.That(Vector3Long.one.Equals((object)Vector3Long.zero), Is.False);
        }

        [Test]
        public void GetHashCodeTest()
        {
            // shouldn't be 0
            Assert.That(Vector3Long.zero.GetHashCode(), !Is.EqualTo(0));
            Assert.That(Vector3Long.one.GetHashCode(), !Is.EqualTo(0));

            // should be same for same vector
            Assert.That(Vector3Long.zero.GetHashCode(), Is.EqualTo(Vector3Long.zero.GetHashCode()));

            // should be different for different vectors
            Assert.That(Vector3Long.zero.GetHashCode(), !Is.EqualTo(Vector3Long.one.GetHashCode()));
        }
#endif
    }
}
