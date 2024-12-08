using System;
using System.Collections.Generic;

namespace Mirror.Components.Experimental{
  /// <summary>
  /// A class that maintains a running minimum over a fixed-size sliding window of integers.
  /// Provides efficient tracking of the minimum value as elements are added and removed from the window.
  /// </summary>
  public class RunningMin{
    // The fixed size of the sliding window
    private readonly int _windowSize;

    // Queue to store the values in the sliding window
    private readonly Queue<int> _values;

    // Stores the current minimum value in the window
    private int _currentMin;

    /// <summary> Gets the current minimum value in the sliding window. </summary>
    public int CurrentMin => _currentMin;

    /// <summary> Gets the current count of elements in the sliding window. </summary>
    public int Count => _values.Count;

    /// <summary> Checks if the sliding window is full. </summary>
    public bool IsFull => _values.Count == _windowSize;

    /// <summary> Returns last added value. </summary>
    public int Last => _values.ToArray()[_values.Count - 1];

    /// <summary> Initializes a new instance of the <see cref="RunningMin"/> class with a specified window size. </summary>
    /// <param name="windowSize">The maximum number of elements in the sliding window.</param>
    public RunningMin(int windowSize = 100) {
      _windowSize = windowSize > 0 ? windowSize : throw new ArgumentException("Sample packets must be greater than zero.");
      _values = new Queue<int>(windowSize);
      _currentMin = int.MaxValue;
    }

    /// <summary>Resets the values and current minimum.</summary>
    public void Reset() {
      _currentMin = int.MaxValue;
      _values.Clear();
    }

    /// <summary> Recalculates the current minimum by iterating through the queue. Only called when necessary to avoid performance overhead. </summary>
    private void UpdateCurrentMin() {
      _currentMin = int.MaxValue;
      foreach (int value in _values)
        if (value < _currentMin)
          _currentMin = value;
    }

    /// <summary> Adds a value to the sliding window. Updates the current minimum as needed. </summary>
    /// <param name="value">The new value to add to the window.</param>
    public void Add(int value) {
      _values.Enqueue(value);
      if (value < _currentMin)
        _currentMin = value;
      // Check if exceeding the window size, if so then remove oldest item
      if (_values.Count > _windowSize) {
        int removedValue = _values.Dequeue();
        // Check oldest value is equal to minimum and is not equal to the new value we need to calculate the current minimum
        if (removedValue == _currentMin && removedValue != value)
          UpdateCurrentMin();
      }
    }
  }
}