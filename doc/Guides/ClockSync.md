# Clock Synchronization

For many algorithms you need the clock to be synchronized between the client and
the server. Mirror does that automatically for you.

To get the current time use this code:

```cs
double now = NetworkTime.time;
```

It will return the same value in the client and the servers. It starts at 0 when
the server starts. Note the time is a double and should never be casted to a
float. Casting this down to a float means the clock will lose precision after
some time:
-   after 1 day, accuracy goes down to 8 ms
-   after 10 days, accuracy is 62 ms
-   after 30 days , accuracy is 250 ms
-   after 60 days, accuracy is 500 ms

Mirror will also calculate the RTT time as seen by the application:

```cs
double rtt = NetworkTime.rtt;
```

You can measure accuracy.

```cs
double time_standard_deviation = NetworkTime.timeSd;
```

for example, if this returns 0.2, it means the time measurements swing up and
down roughly 0.2 s

Network hickups are compensated against by smoothing out the values using EMA.  
You can configure how often you want the the ping to be sent:

```cs
NetworkTime.PingFrequency = 2.0f;
```

You can also configure how many ping results are used in the calculation:

```cs
NetworkTime.PingWindowSize = 10;
```
