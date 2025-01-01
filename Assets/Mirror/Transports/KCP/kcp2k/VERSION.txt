V1.41 [2024-04-28]
- fix: KcpHeader is now parsed safely, handling attackers potentially sending values out of enum range
- fix: KcpClient RawSend may throw ConnectionRefused SocketException when OnDisconnected calls SendDisconnect(), which is fine
- fix: less scary cookie message and better explanation

V1.40 [2024-01-03]
- added [KCP] to all log messages
- fix: #3704 remove old fix for #2353 which caused log spam and isn't needed anymore since the
  original Mirror issue is long gone
- fix: KcpClient.RawSend now returns if socket wasn't created yet
- fix: https://github.com/MirrorNetworking/Mirror/issues/3591 KcpPeer.SendDisconnect now rapid
  fires several unreliable messages instead of sending reliable. Fixes disconnect message not
  going through if the connection is closed & removed immediately after.

V1.39 [2023-10-31]
- fix: https://github.com/MirrorNetworking/Mirror/issues/3611 Windows UDP socket exceptions
  on server if one of the clients died

V1.38 [2023-10-29]
- fix: #54 mismatching cookie race condition. cookie is now included in all messages.
- feature: Exposed local end point on KcpClient/Server
- refactor: KcpPeer refactored as abstract class to remove KcpServer initialization workarounds

V1.37 [2023-07-31]
- fix: #47 KcpServer.Stop now clears connections so they aren't carried over to the next session
- fix: KcpPeer doesn't log 'received unreliable message while not authenticated' anymore.

V1.36 [2023-06-08]
- fix: #49 KcpPeer.RawInput message size check now considers cookie as well
- kcp.cs cleanups

V1.35 [2023-04-05]
- fix: KcpClients now need to validate with a secure cookie in order to protect against
  UDP spoofing. fixes:
  https://github.com/MirrorNetworking/Mirror/issues/3286
  [disclosed by IncludeSec]
- KcpClient/Server: change callbacks to protected so inheriting classes can use them too
- KcpClient/Server: change config visibility to protected

V1.34 [2023-03-15]
- Send/SendTo/Receive/ReceiveFrom NonBlocking extensions.
  to encapsulate WouldBlock allocations, exceptions, etc.
  allows for reuse when overwriting KcpServer/Client (i.e. for relays).

V1.33 [2023-03-14]
- perf: KcpServer/Client RawReceive now call socket.Poll to avoid non-blocking
  socket's allocating a new SocketException in case they WouldBlock.
  fixes https://github.com/MirrorNetworking/Mirror/issues/3413
- perf: KcpServer/Client RawSend now call socket.Poll to avoid non-blocking
  socket's allocating a new SocketException in case they WouldBlock.
  fixes https://github.com/MirrorNetworking/Mirror/issues/3413

V1.32 [2023-03-12]
- fix: KcpPeer RawInput now doesn't disconnect in case of random internet noise

V1.31 [2023-03-05]
- KcpClient: Tick/Incoming/Outgoing can now be overwritten (virtual)
- breaking: KcpClient now takes KcpConfig in constructor instead of in Connect.
  cleaner, and prepares for KcpConfig.MTU setting.
- KcpConfig now includes MTU; KcpPeer now works with KcpConfig's MTU, KcpServer/Client
  buffers are now created with config's MTU.

V1.30 [2023-02-20]
- fix: set send/recv buffer sizes directly instead of iterating to find the limit.
  fixes: https://github.com/MirrorNetworking/Mirror/issues/3390
- fix: server & client sockets are now always non-blocking to ensure main thread never
  blocks on socket.recv/send. Send() now also handles WouldBlock.
- fix: socket.Receive/From directly with non-blocking sockets and handle WouldBlock,
  instead of socket.Poll. faster, more obvious, and fixes Poll() looping forever while
  socket is in error state. fixes: https://github.com/MirrorNetworking/Mirror/issues/2733

V1.29 [2023-01-28]
- fix: KcpServer.CreateServerSocket now handles NotSupportedException when setting DualMode
  https://github.com/MirrorNetworking/Mirror/issues/3358

V1.28 [2023-01-28]
- fix: KcpClient.Connect now resolves hostname before creating peer
  https://github.com/MirrorNetworking/Mirror/issues/3361

V1.27 [2023-01-08]
- KcpClient.Connect: invoke own events directly instead of going through peer,
  which calls our own events anyway
- fix: KcpPeer/Client/Server callbacks are readonly and assigned in constructor
  to ensure they are safe to use at all times.
  fixes https://github.com/MirrorNetworking/Mirror/issues/3337

V1.26 [2022-12-22]
- KcpPeer.RawInput: fix compile error in old Unity Mono versions
- fix: KcpServer sets up a new connection's OnError immediately.
  fixes KcpPeer throwing NullReferenceException when attempting to call OnError
  after authentication errors.
- improved log messages

V1.25 [2022-12-14]
- breaking: removed where-allocation. use IL2CPP on servers instead.
- breaking: KcpConfig to simplify configuration
- high level cleanups

V1.24 [2022-12-14]
- KcpClient: fixed NullReferenceException when connection without a server.
  added test coverage to ensure this never happens again.

V1.23 [2022-12-07]
- KcpClient: rawReceiveBuffer exposed
- fix: KcpServer RawSend uses connection.remoteEndPoint instead of the helper
  'newClientEP'. fixes clients receiving the wrong messages meant for others.
  https://github.com/MirrorNetworking/Mirror/issues/3296

V1.22 [2022-11-30]
- high level refactor, part two.

V1.21 [2022-11-24]
- high level refactor, part one.
  - KcpPeer instead of KcpConnection, KcpClientConnection, KcpServerConnection
  - RawSend/Receive can now easily be overwritten in KcpClient/Server.
    for non-alloc, relays, etc.

V1.20 [2022-11-22]
- perf: KcpClient receive allocation was removed entirely.
  reduces Mirror benchmark client sided allocations from 4.9 KB / 1.7 KB (non-alloc) to 0B.
- fix: KcpConnection.Disconnect does not check socket.Connected anymore.
  UDP sockets don't have a connection.
  fixes Disconnects not being sent to clients in netcore.
- KcpConnection.SendReliable: added OnError instead of logs

V1.19 [2022-05-12]
- feature: OnError ErrorCodes

V1.18 [2022-05-08]
- feature: OnError to allow higher level to show popups etc.
- feature: KcpServer.GetClientAddress is now GetClientEndPoint in order to
  expose more details
- ResolveHostname: include exception in log for easier debugging
- fix: KcpClientConnection.RawReceive now logs the SocketException even if
  it was expected. makes debugging easier.
- fix: KcpServer.TickIncoming now logs the SocketException even if it was
  expected. makes debugging easier.
- fix: KcpClientConnection.RawReceive now calls Disconnect() if the other end
  has closed the connection. better than just remaining in a state with unusable
  sockets.

V1.17 [2022-01-09]
- perf: server/client MaximizeSendReceiveBuffersToOSLimit option to set send/recv
  buffer sizes to OS limit. avoids drops due to small buffers under heavy load.

V1.16 [2022-01-06]
- fix: SendUnreliable respects ArraySegment.Offset
- fix: potential bug with negative length (see PR #2)
- breaking: removed pause handling because it's not necessary for Mirror anymore

V1.15 [2021-12-11]
- feature: feature: MaxRetransmits aka dead_link now configurable
- dead_link disconnect message improved to show exact retransmit count

V1.14 [2021-11-30]
- fix: Send() now throws an exception for messages which require > 255 fragments
- fix: ReliableMaxMessageSize is now limited to messages which require <= 255 fragments

V1.13 [2021-11-28]
- fix: perf: uncork max message size from 144 KB to as much as we want based on
  receive window size.
    fixes https://github.com/vis2k/kcp2k/issues/22
    fixes https://github.com/skywind3000/kcp/pull/291
- feature: OnData now includes channel it was received on

V1.12 [2021-07-16]
- Tests: don't depend on Unity anymore
- fix: #26 - Kcp now catches exception if host couldn't be resolved, and calls
  OnDisconnected to let the user now.
- fix: KcpServer.DualMode is now configurable in the constructor instead of
  using #if UNITY_SWITCH. makes it run on all other non dual mode platforms too.
- fix: where-allocation made optional via virtuals and inheriting
  KcpServer/Client/Connection NonAlloc classes. fixes a bug where some platforms
  might not support where-allocation.

V1.11 rollback [2021-06-01]
- perf: Segment MemoryStream initial capacity set to MTU to avoid early runtime
  resizing/allocations

V1.10 [2021-05-28]
- feature: configurable Timeout
- allocations explained with comments (C# ReceiveFrom / IPEndPoint.GetHashCode)
- fix: #17 KcpConnection.ReceiveNextReliable now assigns message default so it
  works in .net too
- fix: Segment pool is not static anymore. Each kcp instance now has it's own
  Pool<Segment>. fixes #18 concurrency issues

V1.9 [2021-03-02]
- Tick() split into TickIncoming()/TickOutgoing() to use in Mirror's new update
  functions. allows to minimize latency.
  => original Tick() is still supported for convenience. simply processes both!

V1.8 [2021-02-14]
- fix: Unity IPv6 errors on Nintendo Switch
- fix: KcpConnection now disconnects if data message was received without content.
  previously it would call OnData with an empty ArraySegment, causing all kinds of
  weird behaviour in Mirror/DOTSNET. Added tests too.
- fix: KcpConnection.SendData: don't allow sending empty messages anymore. disconnect
  and log a warning to make it completely obvious.

V1.7 [2021-01-13]
- fix: unreliable messages reset timeout now too
- perf: KcpConnection OnCheckEnabled callback changed to a simple 'paused' boolean.
  This is faster than invoking a Func<bool> every time and allows us to fix #8 more
  easily later by calling .Pause/.Unpause from OnEnable/OnDisable in MirrorTransport.
- fix #8: Unpause now resets timeout to fix a bug where Mirror would pause kcp,
  change the scene which took >10s, then unpause and kcp would detect the lack of
  any messages for >10s as timeout. Added test to make sure it never happens again.
- MirrorTransport: statistics logging for headless servers
- Mirror Transport: Send/Receive window size increased once more from 2048 to 4096.

V1.6 [2021-01-10]
- Unreliable channel added!
- perf: KcpHeader byte added to every kcp message to indicate
  Handshake/Data/Ping/Disconnect instead of scanning each message for Hello/Byte/Ping
  content via SegmentEquals. It's a lot cleaner, should be faster and should avoid
  edge cases where a message content would equal Hello/Ping/Bye sequence accidentally.
- Kcp.Input: offset moved to parameters for cases where it's needed
- Kcp.SetMtu from original Kcp.c

V1.5 [2021-01-07]
- KcpConnection.MaxSend/ReceiveRate calculation based on the article
- MirrorTransport: large send/recv window size defaults to avoid high latencies caused
  by packets not being processed fast enough
- MirrorTransport: show MaxSend/ReceiveRate in debug gui
- MirrorTransport: don't Log.Info to console in headless mode if debug log is disabled

V1.4 [2020-11-27]
- fix: OnCheckEnabled added. KcpConnection message processing while loop can now
  be interrupted immediately. fixes Mirror Transport scene changes which need to stop
  processing any messages immediately after a scene message)
- perf: Mirror KcpTransport: FastResend enabled by default. turbo mode according to:
  https://github.com/skywind3000/kcp/blob/master/README.en.md#protocol-configuration
- perf: Mirror KcpTransport: CongestionControl disabled by default (turbo mode)

V1.3 [2020-11-17]
- Log.Info/Warning/Error so logging doesn't depend on UnityEngine anymore
- fix: Server.Tick catches SocketException which happens if Android client is killed
- MirrorTransport: debugLog option added that can be checked in Unity Inspector
- Utils.Clamp so Kcp.cs doesn't depend on UnityEngine
- Utils.SegmentsEqual: use Linq SequenceEqual so it doesn't depend on UnityEngine
=> kcp2k can now be used in any C# project even without Unity

V1.2 [2020-11-10]
- more tests added
- fix: raw receive buffers are now all of MTU size
- fix: raw receive detects error where buffer was too small for msgLength and
  result in excess data being dropped silently
- KcpConnection.MaxMessageSize added for use in high level
- KcpConnection.MaxMessageSize increased from 1200 bytes to to maximum allowed
  message size of 145KB for kcp (based on mtu, overhead, wnd_rcv)

V1.1 [2020-10-30]
- high level cleanup, fixes, improvements

V1.0 [2020-10-22]
- Kcp.cs now mirrors original Kcp.c behaviour
  (this fixes dozens of bugs)

V0.1
- initial kcp-csharp based version