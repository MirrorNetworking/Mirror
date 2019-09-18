# Why TCP by default and not UDP?

It's the year 2019 and every game developer swears by UDP. Yet we chose TCP as default for Mirror. Why is that?

First of all, a quick word about the major differences between UDP and TCP.
-   UDP has lower latency, unreliable and hard to use correctly
-   TCP has higher latency, reliable and easy to use

## TCP (Transmision Control Protocol)

[Tcp](https://en.wikipedia.org/wiki/Transmission_Control_Protocol#Congestion_control) is a protocol built on top of IP. It is by far the most popular protocol on the internet.  Everything you are seeing in this page was sent to your browser via TCP. It is designed to be simple to use and scalable. 

Servers open a TCP port and wait for connections.  Clients send an initial message (handshake) to establish the connection then send data.  Data flows one byte after another, always in the right order, without missing anything. Some of the key features include:

* Reliable: if a packet gets lost,  TCP will resend it. All data is either transmitted successfully or you get an error and the connection is closed.  Applications don't have to worry about missing packets.
* Fragmented: network cards cannot just send 1 MB of data. They can only send small packets of 1.5Kb or less.  If a lot of data is sent by the application, TCP will split it into small packets and reassemble the data on the receiving end.
* Sequenced: If you send data "a" and "b" you will not receive "b" and "a".  TCP guarantees that every byte will arrive in the same order it was sent.
* Connection oriented: TCP has the concept of a connection.  A client sends an initial handshake message.  A connection is considered established until either the client and server decides to disconnect.  Both the client and server get notified when the connection ends and can react accordingly,  for example saving and destroying player object.
* Congestion control: If a server is being overwhelmed,  TCP will throttle the data to avoid congestion collapse.

These are great features that make it very easy for programmers to work with TCP, but they come at a cost:  Latency.  

Suppose an object is moving from point a to b to c.  The server sends 3 messages: move to a, b, c. Suppose b gets lost (wifi drops a lot of packets for example) and c arrives fine. We could skip b and move towards c instead,  but we can't because the operating system won't give us c until b is retransmited.

For this reason, AAA studios consistently prefer UDP for fast paced action games.

## UDP (User Datagram Protocol)

[UDP](https://en.wikipedia.org/wiki/User_Datagram_Protocol) is also a protocol based on IP.  It is used for real time applications such as fast paced action games or voice over ip, where low latency is more important than reliability.

A server opens a port and waits for messages. Clients send messages to the port, and the server may send messages back. Data flows in both ways as individual messages.  

There is no concept of connection, so there is no built in way to determine if a client disconnects. Messages are delivered as soon as possible,  there is no guarantee that the order will be preserved or that they will be delivered at all.  Messages must be small,  typically 1.5Kb or less.  

Mirror does need reliability, fragmentation, sequenced, connections for many things,  so we would not use raw UDP.  We would use a library that implements those features on top of UDP such as [ENet](http://enet.bespin.org/), [LiteNetLib](https://github.com/RevenantX/LiteNetLib) or LLAPI,  typically refered to as RUDP (Reliable UDP)

The obvious question is:  do RUDP libraries just reinventing TCP?  yes, to some degree they do. But the point is that those features are optional and we can send messages without the extra features for low latency data such as movement or voice. 

## Dark ages

Back in 2015 when we started uMMORPG and Cubica, we originally used Unity's built in Networking system aka UNET. UNET used LLAPI, an RUDP library that avoided garbage collection at all costs.

What sounds good in theory, was terrible in practice. We spent about half our work hours from 2015 to 2018 dealing with UNET bugs. There was packet loss, highly complex code due to GC avoidance, synchronization issues, memory leaks and random errors. Most importantly, no decent way to debug any of it.

If a monster didn't spawn on a client, we wouldn't know what caused it.
-   Was the packet dropped by UDP?
-   Was it a bug in the highly complex UNET source code?
-   Was the reliable layer on top of UDP not working as intended?
-   Was the reliable layer actually fully reliable?
-   Did we use the right networking config for the host that we tested it on?
-   Or was it a bug in our own project?

After 3 years in UDP/LLAPI hell, we realized if we ever wanted to finish our games, we would need a networking layer that just works. We could have tried other RUDP transports, but we would end up debugging them instead.

That's why we made Telepathy and Mirror. **Life is short. We just need the damn thing to work.**

## The choice is yours

We acknowledge not everyone will agree with our reasoning. Rather than push our views on users, we made Mirror transport independent. A few months later, Unity did the same thing. You can easily swap out the transport for one of the several RUDP implementations simply by dragging it into your NetworkManager gameobject. Pick whatever works best for you. We recommend you profile your game and collect real world numbers before you make a final decision.

After we made Mirror transport independent,  the community stepped up integrated several RUDP transports with Mirror.  While the default is Telepathy (simple "just works" TCP transport) you can choose among several [transports](../Transports/index.md) or even write your own.

