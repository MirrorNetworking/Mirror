using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkClients
{
    /// <summary>
    /// Tests for OnTransportData() internal branches that require a live
    /// connection:
    ///   • AddBatch failure   (invalid batch, < 8 bytes)
    ///   • Too-short message  (message payload smaller than the 2-byte id)
    ///   • Unknown message id (no registered handler for the message type)
    ///   • isLoadingScene     (processing loop is skipped while loading)
    /// Each error branch is exercised with both exceptionsDisconnect values.
    /// </summary>
    public class NetworkClientTests_OnTransportData : MirrorEditModeTest
    {
        // A zero-field test message registered only in the isLoadingScene test.
        struct LocalTestMessage : NetworkMessage { }

        // Build a properly-framed batch that wraps a single raw message segment.
        // The caller is responsible for building the segment (msgType + optional fields).
        static byte[] MakeValidBatch(ArraySegment<byte> messageSegment)
        {
            // Batcher writes: [double timestamp][varint(size)][bytes]
            Batcher batcher = new Batcher(ushort.MaxValue);
            batcher.AddMessage(messageSegment, 0);
            using NetworkWriterPooled writer = NetworkWriterPool.Get();
            batcher.GetBatch(writer);
            return writer.ToArray();
        }

        // ── Invalid batch (fewer than 8 bytes, can't read the timestamp) ────

        [Test]
        public void OnTransportData_InvalidBatch_ExceptionsDisconnect_LogsError()
        {
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            // Unbatcher.AddBatch requires at least sizeof(double) = 8 bytes.
            // Three bytes → AddBatch returns false → error path.
            LogAssert.Expect(LogType.Error,
                "NetworkClient: failed to add batch, disconnecting.");
            NetworkClient.OnTransportData(
                new ArraySegment<byte>(new byte[] { 1, 2, 3 }),
                Channels.Reliable);
        }

        [Test]
        public void OnTransportData_InvalidBatch_NoExceptionsDisconnect_LogsWarning()
        {
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            NetworkClient.exceptionsDisconnect = false;

            LogAssert.Expect(LogType.Warning,
                "NetworkClient: failed to add batch.");
            NetworkClient.OnTransportData(
                new ArraySegment<byte>(new byte[] { 1, 2, 3 }),
                Channels.Reliable);
        }

        // ── Too-short message (1-byte segment, < IdSize = 2) ─────────────────

        [Test]
        public void OnTransportData_TooShortMessage_ExceptionsDisconnect_LogsError()
        {
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            // A 1-byte message cannot contain the minimum 2-byte message id.
            byte[] batch = MakeValidBatch(new ArraySegment<byte>(new byte[] { 0xAB }));

            LogAssert.Expect(LogType.Error,
                "NetworkClient: received Message was too short (messages should start with message id). Disconnecting.");
            NetworkClient.OnTransportData(new ArraySegment<byte>(batch), Channels.Reliable);
        }

        [Test]
        public void OnTransportData_TooShortMessage_NoExceptionsDisconnect_LogsWarning()
        {
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            NetworkClient.exceptionsDisconnect = false;

            byte[] batch = MakeValidBatch(new ArraySegment<byte>(new byte[] { 0xAB }));

            LogAssert.Expect(LogType.Warning,
                "NetworkClient: received Message was too short (messages should start with message id)");
            NetworkClient.OnTransportData(new ArraySegment<byte>(batch), Channels.Reliable);
        }

        // ── Unknown message id (no handler registered for the type) ──────────

        [Test]
        public void OnTransportData_UnknownMessageId_ExceptionsDisconnect_LogsWarningThenError()
        {
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            // 0xBEEF = 48879 – a msgType with no registered handler.
            // Mirror WriteUShort is little-endian: byte[0]=0xEF, byte[1]=0xBE.
            using NetworkWriterPooled msgWriter = NetworkWriterPool.Get();
            msgWriter.WriteUShort(0xBEEF);
            byte[] batch = MakeValidBatch(msgWriter.ToArraySegment());

            // UnpackAndInvoke logs a warning first, then OnTransportData an error.
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    @"Unknown message id: 48879\..*"));
            LogAssert.Expect(LogType.Error,
                "NetworkClient: failed to unpack and invoke message. Disconnecting.");
            NetworkClient.OnTransportData(new ArraySegment<byte>(batch), Channels.Reliable);
        }

        [Test]
        public void OnTransportData_UnknownMessageId_NoExceptionsDisconnect_LogsWarnings()
        {
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            NetworkClient.exceptionsDisconnect = false;

            using NetworkWriterPooled msgWriter = NetworkWriterPool.Get();
            msgWriter.WriteUShort(0xBEEF);
            byte[] batch = MakeValidBatch(msgWriter.ToArraySegment());

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    @"Unknown message id: 48879\..*"));
            LogAssert.Expect(LogType.Warning,
                "NetworkClient: failed to unpack and invoke message.");
            NetworkClient.OnTransportData(new ArraySegment<byte>(batch), Channels.Reliable);
        }

        // ── isLoadingScene halts the processing loop ─────────────────────────

        [Test]
        public void OnTransportData_HaltsProcessingWhenIsLoadingScene()
        {
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);

            // Register a handler so a matching message would normally be dispatched.
            int handlerCallCount = 0;
            NetworkClient.RegisterHandler<LocalTestMessage>(_ => handlerCallCount++, false);

            // Build a valid batch containing one LocalTestMessage (msgType only, no fields).
            using NetworkWriterPooled msgWriter = NetworkWriterPool.Get();
            msgWriter.WriteUShort(NetworkMessageId<LocalTestMessage>.Id);
            byte[] batch = MakeValidBatch(msgWriter.ToArraySegment());

            // The while-loop guard '!isLoadingScene' prevents any processing.
            NetworkClient.isLoadingScene = true;
            NetworkClient.OnTransportData(new ArraySegment<byte>(batch), Channels.Reliable);

            Assert.That(handlerCallCount, Is.EqualTo(0));
        }
    }
}