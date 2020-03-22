using System;
using NUnit.Framework;
using NSubstitute;

namespace Mirror.Tests
{
    [TestFixture(Category = "NetworkDiagnostics")]
    public class NetworkDiagnosticsTests
    {
        [Test]
        public void TestOnSendEvent()
        {
            Action<NetworkDiagnostics.MessageInfo> outMessageCallback = Substitute.For<Action<NetworkDiagnostics.MessageInfo>>();
            NetworkDiagnostics.OutMessageEvent += outMessageCallback;

            var message = new TestMessage();
            NetworkDiagnostics.OnSend(message, Channels.DefaultReliable, 10, 5);
            var expected = new NetworkDiagnostics.MessageInfo(message, Channels.DefaultReliable, 10, 5);
            outMessageCallback.Received(1).Invoke(Arg.Is(expected));

            NetworkDiagnostics.OutMessageEvent -= outMessageCallback;
        }

        [Test]
        public void TestOnSendZeroCountEvent()
        {
            Action<NetworkDiagnostics.MessageInfo> outMessageCallback = Substitute.For<Action<NetworkDiagnostics.MessageInfo>>();
            NetworkDiagnostics.OutMessageEvent += outMessageCallback;

            var message = new TestMessage();
            NetworkDiagnostics.OnSend(message, Channels.DefaultReliable, 10, 0);
            outMessageCallback.DidNotReceive();

            NetworkDiagnostics.OutMessageEvent -= outMessageCallback;
        }

        [Test]
        public void TestOnReceiveEvent()
        {
            Action<NetworkDiagnostics.MessageInfo> outMessageCallback = Substitute.For<Action<NetworkDiagnostics.MessageInfo>>();
            NetworkDiagnostics.InMessageEvent += outMessageCallback;

            var message = new TestMessage();
            NetworkDiagnostics.OnReceive(message, Channels.DefaultReliable, 10);
            var expected = new NetworkDiagnostics.MessageInfo(message, Channels.DefaultReliable, 10, 1);
            outMessageCallback.Received(1).Invoke(Arg.Is(expected));

            NetworkDiagnostics.InMessageEvent -= outMessageCallback;
        }
    }
}
