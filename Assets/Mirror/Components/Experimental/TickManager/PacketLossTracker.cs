using System;
using System.Collections.Generic;

namespace Mirror.Components.Experimental{
  /// <summary>
  /// A class for tracking packet loss over a sliding window of packets.
  /// It calculates the packet loss rate as a fraction based on a fixed sample size,
  /// and provides both a floating-point representation and a byte-scaled value for the loss.
  /// </summary>
  public class PacketLossTracker{
    // A queue to store recent packet loss values (1 for loss, 0 for received)
    private Queue<int> _packetLossWindow;

    // The current packet loss rate as a float (between 0 and 1)
    private float _loss = 0;

    // Sum of packet loss values in the current window
    private int _packetLossSum = 0;

    // Number of packets in the sample window for calculating loss
    private readonly int _samplePackets;

    /// <summary> Initializes a new instance of the PacketLossTracker class with a specified sample size. </summary>
    /// <param name="samplePackets">The number of packets to track for calculating the loss rate.</param>
    /// <exception cref="ArgumentException">Thrown when samplePackets is zero or negative.</exception>
    public PacketLossTracker(int samplePackets) {
      _samplePackets = samplePackets > 0 ? samplePackets : throw new ArgumentException("Sample packets must be greater than zero.");
      _packetLossWindow = new Queue<int>(_samplePackets);
    }

    /// <summary>
    /// Adds a packet result to the tracker, indicating whether the packet was lost or received.
    /// Updates the loss rate based on the latest window of packet results.
    /// </summary>
    /// <param name="isLost">True if the packet was lost; false if the packet was received.</param>
    public void AddPacket(bool isLost) {
      int lossValue = isLost ? 1 : 0;
      _packetLossWindow.Enqueue(lossValue);
      _packetLossSum += lossValue;

      // Remove the oldest packet when the queue exceeds the max size
      if (_packetLossWindow.Count > _samplePackets)
        _packetLossSum -= _packetLossWindow.Dequeue();

      _loss = (float)_packetLossSum / _samplePackets;
    }

    /// <summary> Gets the current packet loss rate as a floating-point value between 0 and 100. </summary>
    public float Loss => _loss * 100;
  }
}