using Mirror.Tests.NetworkMessagesTests;
using NUnit.Framework;

namespace Mirror.Tests.NetworkReaderWriter
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
    public class CustomReaderWriterTests
    {
        public struct QuestMessage : NetworkMessage
        {
            public MockQuest quest;
        }

        [Test]
        public void TestCustomRW()
        {
            QuestMessage message = new QuestMessage {quest = new MockQuest(100)};

            byte[] data = NetworkMessagesTest.PackToByteArray(message);
            QuestMessage unpacked = NetworkMessagesTest.UnpackFromByteArray<QuestMessage>(data);
            Assert.That(unpacked.quest.Id, Is.EqualTo(100));
        }
    }
}
