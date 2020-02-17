# Cheat Safe local player Movement (by vis2k)

**NetworkTransform** has two modes, either server authoritative (by default), or client authoritative (where players can cheat).

For server authoritative it's meant to be used like this:
Code (CSharp):
```
    player sends CmdPleaseMoveMeTo(xyz);
    server checks if this is a valid move.
    server moves it there
    NetworkTransform automatically syncs it to clients
```

If you want to set transform.position on the local player, then you need to use NetworkTransform's clientAuthority option - which allows cheating as you know.

If you don't want cheating,  need to use server authority. In a perfect world, the above code would be just fine. In our world, you have latency. So when you press the 'W' key and ask the server to please move you forward by 1 meter, then it takes way too long for the server to reply. It works, but it feels terrible.

What you want is either client side prediction, or rubberband movement.

**Client side prediction** means: ask the server to please move you forward by 1 meter. Then instead of waiting for the reply, take a guess on what the answer will be (probably yes unless there is a wall in front of you), then already move forward hoping that the server ends up with the same end position. This can work. It's not easy to get right for the local player though. There are still latencies and you are still trying to make imperfect predictions which will occasionally be wrong, causing position resets. Note that client side prediction is cheat safe, because the server still doesn't trust the client. The client just makes some assumptions but the server has the last word on it.

**Another option is rubberbanding**. This is usually used in MMOs, and I had good success with it in my [uMMORPG](https://assetstore.unity.com/packages/templates/systems/ummorpg-components-edition-159401) and [uSurvival](https://assetstore.unity.com/packages/templates/systems/usurvival-95015) assets. Unlike client side prediction, you don't ask the server to please move you forward 1 meter and then wait for the result. Instead, you move forward 1 meter when ever you want, then you tell the server 'I moved forward 1 meter', then the server checks if this was a valid move. If it wasn't (if you cheated through a wall, or moved too fast), then the server will reject the move, keep the previously valid position and notify the player that it was rejected. This is still cheat safe. Imho this is the 'easiest' way to do it.

Neither option is easy. In fact, it's very difficult. As far as I know, the character controller in my assets is the only one for Mirror that does it. And even that one isn't completely finished yet.

We don't have a client side prediction or a rubberbanding NetworkTransform in Mirror by default, because those depend a lot on your game. For example, you might allow different movement speeds based on different states. Some games have crouching, some have running, some have jumping, some allow movement while jumping where others don't. Some allow vertical climbing. So what move is valid really depends on your game.

_Note that **there is a third option**, which is what a lot of AAA games / MMOs do. You use the regular NetworkTransform with clientAuthority (where players could cheat), and then you add anti cheat protections to the client. Depending on how successful your game will be, you will need really good anti cheat protection to get away with this. Simple isDebuggerPresent checks won't do, most hackers can bypass those in 5 minutes. Imho you would need at least virtualization with tools like Themida.
A lot of big games use this method because both client side prediction and rubberbanding are extremely difficult to get right. Whenever you hear about players speedhacking in popular games like GTA, it's because the developers chose this way (the lazy way). Personally I don't like this method. It's understandable why people try to get away with it considering deadlines and budgets, but in the end people will cheat and then you are in trouble._