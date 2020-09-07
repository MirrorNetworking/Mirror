using NUnit.Framework;

namespace Mirror.Tests
{
    public class MockQuest
    {
        public int Id { get; set; }

        public MockQuest(int id)
        {
            Id = id;
        }

        public MockQuest()
        {
            Id = 0;
        }
    }

    public static class MockQuestReaderWriter
    {
        public static void WriteQuest(this NetworkWriter writer, MockQuest quest)
        {
            writer.WritePackedInt32(quest.Id);
        }
        public static MockQuest ReadQuest(this NetworkReader reader)
        {
            return new MockQuest(reader.ReadPackedInt32());
        }
    }

    [TestFixture]
    public class CustomRWTest
    {

        class QuestMessage : MessageBase
        {
            public MockQuest quest;
        }

        [Test]
        public void TestCustomRW()
        {
            var message = new QuestMessage
            {
                quest = new MockQuest(100)
            };

            byte[] data = MessagePacker.Pack(message);

            QuestMessage unpacked = MessagePacker.Unpack<QuestMessage>(data);

            Assert.That(unpacked.quest.Id, Is.EqualTo(100));
        }
    }
}
