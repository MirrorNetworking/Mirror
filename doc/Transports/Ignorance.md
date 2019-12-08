# Ignorance

## What is Ignorance?
Ignorance is a reliable UDP transport layer that utilizes the native ENET C Networking library via a [custom fork of ENet-CSharp](https://github.com/SoftwareGuy/ENet-CSharp) providing an reliable and unreliable sequenced UDP transport for both 64Bit desktop operating systems (Windows, Mac OS and Linux) and Mobile OSes (Apple iOS and Android). It also supports up to 255 channels and 4096 clients connected at one time.

ENET is a solid reliable UDP C++ network library that is mature and stable. Unity's LLAPI needs a replacement. Ignorance was designed with that goal in mind - fill the gap and provide a solid, performant RUDP transport for Mirror.

## Why Ignorance over the Unity LLAPI?
Unity's old LLAPI was horridly inefficient, and lots of testing has shown that you will get reduced performance using Unity LLAPI in your project. This is due to the design of the old networking code - Unity Tech made "by design" decisions and poor bug fixes that were seen to other developers as band-aids over a gaping wound. They did not care about performance or bug fixes.

Unity LLAPI was also closed source, meaning the Mirror developers could not take a knife to it and make it better. This is where the concept of Ignorance took shape.

## Who develops Ignorance?
[Coburn](http://github.com/softwareguy) is the lead developer of the transport. Oiran Studio actively uses this transport for networked game projects. It is currently also being utilized by some game projects, where you can find on the Mirror Discord server.

## Why would I want to use reliable UDP over TCP?
- if you have realtime communications that you need speed over reliability (VoIP...)
- if you need channels
- if you need custom channel send types
- if you need a data hose for your game (a first person shooter, racing game, etc)

## Why wouldn't I want to use reliable UDP over TCP?
- if you have **mission critical** things (as in, data **NEEDS** to go from A and B, no exceptions)
- if you need fully reliable network protocol
- if you're paranoid
- if you're making a Minecraft-like game and need to keep everyone in sync

## I want to know more about reliable UDP...
A little explanation is required. UDP is best described as a "shattershot" data transmission protocol, which means you just spray and pray that packets at a destination and hope for the best. The remote destination may or may not receive those packets, nor are they going to be in order. For example, if you have a packet stream that is:
```
1 2 3 4 5 6 7
```
...then it may end up like any of the following on the other end due to packets arriving out of order. A dot in the following example means that packet went missing.
```
7 6 1 3 2 4 5
7 6 . . 4 . 1
. . . . 1 2 3
1 2 3 5 4 6 7
```

For example, say you lost a packet and that contained a player's health update. Everyone else might know they took 69 damage, but that client will still have the old value of say, 72 health. Without reliable UDP, you can become out of sync very quickly. When you're out of sync, the game is over - everything will start operating very strangely.

## Sequencing and Reliable Delivery

### Sequencing
**Sequencing** basically tags packets so they know what number they are when being dispatched. So if you send packets `100, 101, 102` to the remote destination, the other end will reconstruct the packet in that order rather than in a different order (like `101, 100, 102`). If a packet is missing, it'll be skipped but the network library will take note that it's missing and compensate.

**Reliable** mode just tells ENET to send this while waiting for the remote to acknowledge packet reception, before claiming it was 'lost'. ENET will still classify said packets as lost if it doesn't hear back from the remote, but it will retransmit them to compensate for lossy connections or high latency situations. Reliable mode tries to emulate some of TCP's resending if not acknowledged in time, but as UDP does not have all the overhead TCP protocol has, it adds some packet overhead.

Ignorance comes with two channels in both Reliable and Unreliable mode by default. There are other channel modes that developers can test as different ones might suit different loads, but the average person does not need to worry about this. Ignorance comes with sane defaults out of the box.

## Does Ignorance support Websockets?
No, it does not. Mirror comes with built-in websockets support.

## Where can I get Ignorance?
[Grab the latest build from the releases page on the Ignorance repository](https://github.com/SoftwareGuy/Ignorance). Simply import the Unity Package from the release you downloaded.

## Where can I get support?
You can get support by opening a issue ticket on the [Ignorance repository issue tracker](https://github.com/SoftwareGuy/Ignorance/issues) or the #ignorance channel in the Mirror Discord server.

## I still don't understand what this transport is, my head is spinning, help!
Come by the Discord and we'll do our best to explain it in plain English.
