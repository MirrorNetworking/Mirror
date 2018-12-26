# Networking Tips for Mobile devices.

Mirror is fully compatible across Desktop, iOS and Android devices, players on all these platforms can play your multiplayer game together.

You might want to implement special measures if your game is mainly to be used with Wi-Fi or cellular networks. On some mobile devices, the networking chip might be a performance bottleneck, because pings between mobile devices (or between mobile devices and desktops) can be about 40–60ms, even on high-performance Wi-Fi network, and you may observe some delays of over 200ms, despite a low average ping on high performance wifi.

For players to play your game simultaneously from both desktop and mobile platforms (over Wi-Fi or cellular networks), your game server should have a public IP address accessible through the internet, or use the Matchmaker and Relay services.

Note: EDGE / 3G data connections go to sleep very quickly when no data is sent. Therefore, you might sometimes need to “wake” the connection. If you are using the WWW class make sure you connect to your site (and yield until it finishes) before making the networking connection.
