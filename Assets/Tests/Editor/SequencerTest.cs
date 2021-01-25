using NUnit.Framework;

namespace Mirror
{
    public class SequencerTest
    {
        [Test]
        public void TestNext()
        {
            var sequencer = new Sequencer(3);
            Assert.That(sequencer.Next(), Is.EqualTo(1));
        }

        [Test]
        public void TestBits()
        {
            var sequencer = new Sequencer(3);
            Assert.That(sequencer.Bits, Is.EqualTo(3));
        }

        [Test]
        public void TestWrap()
        {
            var sequencer = new Sequencer(2);
            Assert.That(sequencer.Next(), Is.EqualTo(1));
            Assert.That(sequencer.Next(), Is.EqualTo(2));
            Assert.That(sequencer.Next(), Is.EqualTo(3));
            Assert.That(sequencer.Next(), Is.EqualTo(0),
                "2 bit sequencer should wrap after 4 numbers");
        }

        [Test]
        public void TestDistanceAtBegining()
        {
            var sequencer = new Sequencer(8);
            Assert.That(sequencer.Distance(0, 8), Is.EqualTo(-8));
        }

        [Test]
        public void TestNegativeDistance()
        {
            var sequencer = new Sequencer(8);
            Assert.That(sequencer.Distance(8, 0), Is.EqualTo(8));
        }

        [Test]
        public void TestWrappingDistance()
        {
            var sequencer = new Sequencer(8);
            Assert.That(sequencer.Distance(254, 4), Is.EqualTo(-6));
        }
    }
}