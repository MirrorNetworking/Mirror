using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkClients
{
    /// <summary>
    /// Tests for Send() error paths, Disconnect() guard, OnTransportData null-connection
    /// guard, and the OnTransportError / OnTransportException event forwarding paths.
    /// </summary>
    public class NetworkClientTests_TransportAndSend : MirrorEditModeTest
    {
        // Each test sets up only the server / connection state it actually needs.

        // ── Send() error paths ───────────────────────────────────────────────

        [Test]
        public void Send_LogsErrorWhenConnectionIsNull()
        {
            // Default state: never connected, so connection == null.
            LogAssert.Expect(LogType.Error, "NetworkClient Send with no connection");
            NetworkClient.Send(new ReadyMessage());
        }

        [Test]
        public void Send_LogsErrorWhenNotConnected()
        {
            NetworkServer.Listen(1);
            NetworkClient.ConnectHost(); // connection != null, connectState = Connected

            // Force an intermediate state to exercise the "not yet connected" branch.
            NetworkClient.connectState = ConnectState.Connecting;

            LogAssert.Expect(LogType.Error, "NetworkClient Send when not connected to a server");
            NetworkClient.Send(new ReadyMessage());
        }

        // ── Disconnect() guard ───────────────────────────────────────────────

        [Test]
        public void Disconnect_IsNoOpWhenStateIsNone()
        {
            Assert.That(NetworkClient.connectState, Is.EqualTo(ConnectState.None));
            NetworkClient.Disconnect();
            // State must be unchanged — no side effects from the early return.
            Assert.That(NetworkClient.connectState, Is.EqualTo(ConnectState.None));
        }

        [Test]
        public void Disconnect_IsNoOpWhenAlreadyDisconnecting()
        {
            NetworkClient.connectState = ConnectState.Disconnecting;
            NetworkClient.Disconnect();
            Assert.That(NetworkClient.connectState, Is.EqualTo(ConnectState.Disconnecting));
        }

        [Test]
        public void Disconnect_IsNoOpWhenAlreadyDisconnected()
        {
            NetworkClient.connectState = ConnectState.Disconnected;
            NetworkClient.Disconnect();
            Assert.That(NetworkClient.connectState, Is.EqualTo(ConnectState.Disconnected));
        }

        // ── OnTransportData null-connection guard ─────────────────────────────────────

        [Test]
        public void OnTransportData_LogsErrorWhenConnectionIsNull()
        {
            // connection is null by default; must log and return without throwing.
            LogAssert.Expect(LogType.Error,
                "Skipped Data message handling because connection is null.");
            NetworkClient.OnTransportData(
                new ArraySegment<byte>(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
                Channels.Reliable);
        }

        // ── Transport error / exception event forwarding ──────────────────────────────────

        [Test]
        public void OnTransportError_InvokesOnErrorEvent()
        {
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            TransportError receivedError = default;
            string receivedReason = null;
            NetworkClient.OnErrorEvent = (error, reason) =>
            {
                receivedError = error;
                receivedReason = reason;
            };

            // OnTransportError always logs a warning before forwarding the event.
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("Client Transport Error.*"));
            Transport.active.OnClientError?.Invoke(TransportError.Timeout, "timeout");

            Assert.That(receivedError, Is.EqualTo(TransportError.Timeout));
            Assert.That(receivedReason, Is.EqualTo("timeout"));
        }

        [Test]
        public void OnTransportException_InvokesOnTransportExceptionEvent()
        {
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            Exception receivedException = null;
            NetworkClient.OnTransportExceptionEvent = ex => receivedException = ex;

            var testException = new Exception("test");

            // OnTransportException always logs a warning before forwarding the event.
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("Client Transport Exception.*"));
            Transport.active.OnClientTransportException?.Invoke(testException);

            Assert.That(receivedException, Is.SameAs(testException));
        }
    }
}