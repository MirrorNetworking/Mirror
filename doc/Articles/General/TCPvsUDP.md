# TCP vs UDP

TCP and UDP are protocol used to send information over the internet. 

Key difference between UDP and TCP
-   TCP has higher latency, reliable
-   UDP has lower latency, unreliable


## TCP (Transmission Control Protocol)

[Tcp](https://en.wikipedia.org/wiki/Transmission_Control_Protocol) is a protocol built on top of IP. It is by far the most popular protocol on the internet. Everything you are seeing in this page was sent to your browser via TCP. It is designed to be simple to use and scalable. 

Servers open a TCP port and wait for connections. Clients send an initial message (handshake) to establish the connection then send data. 

Some of the key features include

* Reliable: if a packet gets lost, TCP will resend it. All data is either transmitted successfully or you get an error and the connection is closed. Applications don't have to worry about missing packets.
* Fragmented: network cards cannot just send 1 MB of data. They can only send small packets of 1.5Kb or less. If a lot of data is sent by the application, TCP will split it into small packets and reassemble the data on the receiving end.
* Sequenced: If you send data "a" and "b" you will not receive "b" and "a". TCP guarantees that every byte will arrive in the same order it was sent.
* Connection oriented: TCP has the concept of a connection. A client sends an initial handshake message. A connection is considered established until either the client and server decides to disconnect. Both the client and server get notified when the connection ends and can react accordingly,  for example saving and destroying player object.
* Congestion control: If a server is being overwhelmed,  TCP will throttle the data to avoid congestion collapse.

These are great features that make it very easy for programmers to work with TCP, but they come at a cost:  Latency. 

Suppose an object is moving from point a to b to c. The server sends 3 messages: move to a, b, c. Suppose b gets lost (wifi drops a lot of packets for example) and c arrives fine. We could skip b and move towards c instead,  but we can't because the operating system won't give us c until b is retransmitted.

For this reason, AAA studios consistently prefer UDP for fast paced action games.

## UDP (User Datagram Protocol)

[UDP](https://en.wikipedia.org/wiki/User_Datagram_Protocol) is also a protocol based on IP. It is used for real time applications such as fast paced action games or voice over ip, where low latency is more important than reliability.

A server opens a port and waits for messages. Clients send messages to the port, and the server may send messages back. Data flows in both ways as individual messages. 

There is no concept of connection, so there is no built in way to determine if a client disconnects. Messages are delivered as soon as possible,  there is no guarantee that the order will be preserved or that they will be delivered at all. Messages must be small,  typically 1.5Kb or less. 

Mirror does need reliability, fragmentation, sequenced, connections for many things,  so we would not use raw UDP. We would use a library that implements those features on top of UDP such as [ENet](http://enet.bespin.org/), [LiteNetLib](https://github.com/RevenantX/LiteNetLib) or LLAPI,  typically referred to as RUDP (Reliable UDP)

The obvious question is:  do RUDP libraries just reinventing TCP?  yes, to some degree they do. But the point is that those features are optional and we can send messages without the extra features for low latency data such as movement or voice. 

## The choice is yours

Mirror is transport independent, they can simply by added to your NetworkManager GameObject. Mirror comes with 2 built in transports to pick from, [Telepathy](../Transports/Telepathy.md) and [Ninja WebSockets](../Transports/WebSockets.md). See the [transports](../Transports/index.md) page for more about transports.

Pick whatever works best for you. We recommend you profile your game and collect real world numbers before you make a final decision.
