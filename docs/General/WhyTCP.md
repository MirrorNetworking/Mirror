# Why TCP by default and not UDP?

## The same old Discussion

It's the year 2018 and every game developer swears by UDP. Yet we chose TCP as default for Mirror. Why is that?

UDP vs. TCP, the technical aspects

First of all, a quick word about the major differences between UDP and TCP.

-   UDP has lower latency and is unreliable by default and hard to use correctly
-   TCP has higher latency and is reliable by default and easy to use

Now instead of having yet another technical UDP vs. TCP discussion, let's take a look at a real world example to see why we chose TCP over UDP.

## That Game again

Back in 2011, some guy named Markus Persson (aka Notch) created arguably the biggest multiplayer game of all time. The game is called Minecraft.

Minecraft uses TCP, but why is that? Nobody knows, except Markus Persson.

But we can make an educated guess.

## On Java vs. C++

But wait, let's go back a bit further. Minecraft was written in Java, which is outrageous given that back in 2011 every game developer used to swear by C++.

Here are the major differences between C++ and Java:

-   C++ has a lower footprint, it's faster and it's hard to use correctly
-   Java is slow, uses ungodly amounts of memory and is easy to use

That discussion sounds oddly familiar. Speed vs. ease of use, just like UDP vs. TCP.

## Why?

Okay so, why did Notch chose Java instead of C++ and TCP instead of UDP, given that they are both so much slower than their counter parts?

Well, obviously because Notch is an idiot and knows nothing about game development.

But wait, something doesn't add up. Every kid on the planet plays Minecraft and right now there are thousands of people who are having a blast building shelters, surviving zombies at night, making friends and occasionally falling in love.

Oh, and Notch is a billionaire now.

All the evidence points us to the fact that back in 2011, Notch knew something that others didn't.

## The Riddle

The answer to the riddle is the question about optimization.

Notch never optimized for performance. That's why he didn't care about C++ or UDP.

Notch optimized for probability of success. That's why he chose Java and TCP.

And it worked. What good would it be if Minecraft ran at twice the framerate and half the latency without ever seeing the light of day?

## Summary

Back in 2015 when we started uMMORPG and Cubica, we originally used Unity's built in Networking system aka UNET. UNET used UDP and avoided garbage collection at all costs.

What sounds good in theory, was terrible in practice. We spent about half our work hours from 2015 to 2018 dealing with UNET bugs. There was packet loss, highly complex code due to GC avoidance, synchronization issues, memory leaks and random errors. Most importantly, no decent way to debug any of it.

If a monster didn't spawn on a client, we wouldn't know what caused it.

-   Was the packet dropped by UDP?
-   Was it a bug in the highly complex UNET source code?
-   Was the reliable layer on top of UDP not working as intended?
-   Was the reliable layer actually fully reliable?
-   Did we use the right networking config for the host that we tested it on?
-   Or was it a bug in our own project?

After 3 years in UDP hell, we realized what Notch had realized a long time ago: if we ever wanted to finish our games, we would need a networking layer that just works.

That's why we made Telepathy and Mirror. **Life is short. We just need the damn thing to work.**

We acknowledge not everyone will agree with our reasoning. Rather than push our views on users, we made Mirror transport independent.  You can easily swap out the transport for one of the several RUDP implementations simply by dragging it into your NetworkManager gameobject. Pick whatever works best for you. We recommend you profile your game and collect real world numbers before you make a final decision.
