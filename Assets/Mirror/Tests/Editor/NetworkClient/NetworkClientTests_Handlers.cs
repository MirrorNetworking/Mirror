using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkClients
{
    /// <summary>Tests for RegisterHandler, ReplaceHandler, UnregisterHandler.</summary>
    public class NetworkClientTests_Handlers : MirrorEditModeTest
    {
        // Minimal message type used only within these tests.
        struct TestMessage : NetworkMessage { }

        [Test]
        public void RegisterHandler_AddsHandlerToDict()
        {
            NetworkClient.RegisterHandler<TestMessage>(_ => { });
            Assert.That(NetworkClient.handlers.ContainsKey(NetworkMessageId<TestMessage>.Id), Is.True);
        }

        [Test]
        public void RegisterHandler_LogsWarningWhenAlreadyRegistered()
        {
            NetworkClient.RegisterHandler<TestMessage>(_ => { });
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("NetworkClient.RegisterHandler replacing handler.*"));
            NetworkClient.RegisterHandler<TestMessage>(_ => { });
        }

        [Test]
        public void RegisterHandler_WithChannelId_AddsHandlerToDict()
        {
            NetworkClient.RegisterHandler<TestMessage>((msg, channelId) => { });
            Assert.That(NetworkClient.handlers.ContainsKey(NetworkMessageId<TestMessage>.Id), Is.True);
        }

        [Test]
        public void RegisterHandler_WithChannelId_LogsWarningWhenAlreadyRegistered()
        {
            NetworkClient.RegisterHandler<TestMessage>((msg, channelId) => { });
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("NetworkClient.RegisterHandler replacing handler.*"));
            NetworkClient.RegisterHandler<TestMessage>((msg, channelId) => { });
        }

        [Test]
        public void ReplaceHandler_SilentlyReplacesExistingHandler()
        {
            NetworkClient.RegisterHandler<TestMessage>(_ => { });
            // ReplaceHandler must NOT produce a warning
            NetworkClient.ReplaceHandler<TestMessage>(_ => { });
            Assert.That(NetworkClient.handlers.ContainsKey(NetworkMessageId<TestMessage>.Id), Is.True);
        }

        [Test]
        public void ReplaceHandler_WithChannelId_SilentlyReplacesExistingHandler()
        {
            NetworkClient.RegisterHandler<TestMessage>((msg, channelId) => { });
            // ReplaceHandler must NOT produce a warning
            NetworkClient.ReplaceHandler<TestMessage>((msg, channelId) => { });
            Assert.That(NetworkClient.handlers.ContainsKey(NetworkMessageId<TestMessage>.Id), Is.True);
        }

        [Test]
        public void UnregisterHandler_ReturnsTrueAndRemovesFromDict()
        {
            NetworkClient.RegisterHandler<TestMessage>(_ => { });
            bool result = NetworkClient.UnregisterHandler<TestMessage>();
            Assert.That(result, Is.True);
            Assert.That(NetworkClient.handlers.ContainsKey(NetworkMessageId<TestMessage>.Id), Is.False);
        }

        [Test]
        public void UnregisterHandler_ReturnsFalseWhenHandlerNotRegistered()
        {
            bool result = NetworkClient.UnregisterHandler<TestMessage>();
            Assert.That(result, Is.False);
        }
    }
}