# Telepathy

Simple, message based, MMO Scale TCP networking in C\#. And no magic.
- Telepathy was designed with the [KISS Principle](https://en.wikipedia.org/wiki/KISS_principle) in mind.  
- Telepathy is fast and extremely reliable, designed for [MMO](https://assetstore.unity.com/packages/templates/systems/ummorpg-51212) scale Networking.  
- Telepathy uses framing, so anything sent will be received the same way.  
- Telepathy is raw C\# and can be used in Unity3D too.
- Telepathy is available on [GitHub](https://github.com/vis2k/Telepathy.md)

## What makes Telepathy special?

Telepathy was originally designed for [uMMORPG](https://assetstore.unity.com/packages/templates/systems/ummorpg-51212) after 3 years in UDP hell.

We needed a library that is:
-   Stable & Bug free: Telepathy uses only 400 lines of code. There is no magic.
-   High performance: Telepathy can handle thousands of connections and packages.
-   Concurrent: Telepathy uses one thread per connection. It can make heavy use of multi core processors.
-   Simple: Telepathy takes care of everything. All you need to do is call Connect/GetNextMessage/Disconnect.
-   Message based: if we send 10 and then 2 bytes, then the other end receives 10 and then 2 bytes, never 12 at once.

MMORPGs are insanely difficult to make and we created Telepathy so that we would never have to worry about low level Networking again.

## What about...
-   Async Sockets: didn't perform better in our benchmarks.
-   ConcurrentQueue: .NET 3.5 compatibility is important for Unity. Wasn't faster than our SafeQueue anyway.
-   UDP vs. TCP: Minecraft and World of Warcraft are two of the biggest multiplayer games of all time and they both use TCP networking. There is a reason for that.
