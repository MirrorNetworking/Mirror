using NUnit.Framework;

namespace Mirror.Tests
{
    public class MockQuest
    {
        public int Id;

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
            writer.WriteInt(quest.Id);
        }
        public static MockQuest WriteQuest(this NetworkReader reader)
        {
            return new MockQuest(reader.ReadInt());
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
            QuestMessage message = new QuestMessage
            {
                quest = new MockQuest(100)
            };

            byte[] data = MessagePackingTest.PackToByteArray(message);
            QuestMessage unpacked = MessagePackingTest.UnpackFromByteArray<QuestMessage>(data);
            Assert.That(unpacked.quest.Id, Is.EqualTo(100));
        }
    }
}
