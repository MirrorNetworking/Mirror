# TCP vs UDP

TCP and UDP are protocol used to send information over the internet. 

Key difference between UDP and TCP
-   TCP has higher latency, reliable
-   UDP has lower latency, unreliable


## [TCP (Transmission Control Protocol)](https://en.wikipedia.org/wiki/Transmission_Control_Protocol)

TCP is the most popular protocol on the internet. TCP is used for HTTP, SSH, FTP, and many more.

TCP features make it easy for programers to work with TCP but at the cost of latency.

TCP is better for slower paced games where latency isn't important.

#### Key features include

* **Reliable:** Applications don't have to worry about missing packets. If a packet gets lost, TCP will resend it. All data is either transmitted successfully or you get an error and the connection is closed. 
* **Sequenced:** TCP guarantees that every message will arrive in the same order it was sent. If you send "a" then "b" you will receive "a" then "b" on the other side as well.
* **Connection oriented:** TCP has the concept of a connection. A connection will say open until either the client or server decides to close it. Both the client and server get notified when the connection ends.
* **Congestion control:** If a server is being overwhelmed, TCP will throttle the data to avoid congestion collapse.


#### Transports

* [Telepathy](https://mirror-networking.com/docs/Articles/Transports/Telepathy.html)
* [Apathy](https://mirror-networking.com/apathy/)
* [WebGL](https://mirror-networking.com/docs/Articles/Transports/WebSockets.html)

## [UDP (User Datagram Protocol)](https://en.wikipedia.org/wiki/User_Datagram_Protocol)

UDP is used for real time applications such as fast paced action games or voice over ip, where low latency is more important than reliability.

UDP features allow a greater control of how data is sent allowing non-critical data to be send faster.

UDP is better for fast paced games where latency is important and if a few packets are lost the game can recover.

#### Key features include

* **Low Latency:** UDP is faster because it doesn't need to wait for acknowledge packets, instead it can send keep sending new pacakges one after the other.
* **Channel support:** Channels allow for different delivery types. One channel can be used for critical data that needs to get to the destination, while a different channel can just be specified by send and forget without any reliability.
* **Different packet types:** Reliable Ordered, Reliable Unordered, Unreliable, and more depending on the implementation

#### Transports

* [Ignorance](https://mirror-networking.com/docs/Articles/Transports/Ignorance.html)
* [LiteNetLib](https://mirror-networking.com/docs/Articles/Transports/LiteNetLib4Mirror.html)

## The choice is yours

Mirror is transport independent, they can simply by added to your NetworkManager GameObject. Mirror comes with 2 built in transports to pick from, [Telepathy](../Transports/Telepathy.md) and [Ninja WebSockets](../Transports/WebSockets.md). See the [transports](../Transports/index.md) page for more about transports.

Pick whatever works best for you. We recommend you profile your game and collect real world numbers before you make a final decision.
