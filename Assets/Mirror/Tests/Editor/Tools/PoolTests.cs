using NUnit.Framework;

namespace Mirror.Tests.Tools
{
    public class PoolTests
    {
        Pool<string> pool;

        [SetUp]
        public void SetUp()
        {
            pool = new Pool<string>(() => "new string", 0);
        }

        [TearDown]
        public void TearDown()
        {
            pool = null;
        }

        [Test]
        public void TakeFromEmpty()
        {
            // taking from an empty pool should give us a completely new string
            Assert.That(pool.Get(), Is.EqualTo("new string"));
        }

        [Test]
        public void ReturnAndTake()
        {
            // returning and then taking should get the returned one, not a
            // newly generated one.
            pool.Return("returned");
            Assert.That(pool.Get(), Is.EqualTo("returned"));
        }

        [Test]
        public void ReturnNull()
        {
            // make sure we can't accidentally insert null values into the pool.
            // debugging this would be hard since it would only show on get().
            Assert.That(() => pool.Return(null), Throws.ArgumentNullException);
            Assert.That(pool.Count, Is.EqualTo(0));
        }

        [Test]
        public void Count()
        {
            Assert.That(pool.Count, Is.EqualTo(0));
            pool.Return("returned");
            Assert.That(pool.Count, Is.EqualTo(1));
        }
    }
}
