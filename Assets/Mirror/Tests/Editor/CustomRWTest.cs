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
            writer.WriteInt32(quest.Id);
        }
        public static MockQuest WriteQuest(this NetworkReader reader)
        {
            return new MockQuest(reader.ReadInt32());
        }
    }

    [TestFixture]
    public class CustomRWTest
    {

        public struct QuestMessage : NetworkMessage
        {
            public MockQuest quest;
        }

        [Test]
        public void TestCustomRW()
        {
            QuestMessage message = new QuestMessage()
            {
                quest = new MockQuest(100)
            };

            byte[] data = MessagePackerTest.PackToByteArray(message);

            QuestMessage unpacked = MessagePacker.Unpack<QuestMessage>(data);

            Assert.That(unpacked.quest.Id, Is.EqualTo(100));
        }
    }
}
