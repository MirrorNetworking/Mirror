using System;
using System.Collections.Generic;

namespace Mirror.Components.Experimental{
  /// <summary>
  /// A class that maintains a running maximum over a fixed-size sliding window of integers.
  /// Provides efficient tracking of the maximum value as elements are added and removed from the window.
  /// </summary>
  public class RunningMax{
    // The fixed size of the sliding window
    private readonly int _windowSize;

    // The default value to calculate the maximum from
    private readonly int _defaultMin;
    
    // Queue to store the values in the sliding window
    private readonly Queue<int> _values;

    // Stores the current maximum value in the window
    private int _currentMax;

    /// <summary> Gets the current maximum value in the sliding window. </summary>
    public int CurrentMax => _currentMax;

    /// <summary> Gets the current count of elements in the sliding window. </summary>
    public int Count => _values.Count;

    /// <summary> Checks if the sliding window is full. </summary>
    public bool IsFull => _values.Count == _windowSize;

    /// <summary> Returns last added value. </summary>
    public int Last => _values.ToArray()[_values.Count - 1];

    /// <summary> Initializes a new instance of the <see cref="RunningMax"/> class with a specified window size. </summary>
    /// <param name="windowSize">The maximum number of elements in the sliding window.</param>
    /// <param name="defaultMax">The default number to calculate from (defaults to int.MinValue).</param>
    public RunningMax(int windowSize = 100, int defaultMax = int.MinValue) {
      _windowSize = windowSize > 0 ? windowSize : throw new ArgumentException("Sample packets must be greater than zero.");
      _values = new Queue<int>(windowSize);
      _defaultMin = defaultMax;
      _currentMax = _defaultMin;
    }

    /// <summary>Resets the values and current maximum.</summary>
    public void Reset() {
      _currentMax = _defaultMin;
      _values.Clear();
    }

    /// <summary> Recalculates the current maximum by iterating through the queue. Only called when necessary to avoid performance overhead. </summary>
    private void UpdateCurrentMax() {
      _currentMax = _defaultMin;
      foreach (int value in _values)
        if (value > _currentMax)
          _currentMax = value;
    }

    /// <summary> Adds a value to the sliding window. Updates the current maximum as needed. </summary>
    /// <param name="value">The new value to add to the window.</param>
    public void Add(int value) {
      _values.Enqueue(value);
      if (value > _currentMax)
        _currentMax = value;
      // Check if exceeding the window size, if so then remove oldest item
      if (_values.Count > _windowSize) {
        int removedValue = _values.Dequeue();
        // Check oldest value is equal to maximum and is not equal to the new value we need to calculate the current maximum
        if (removedValue == _currentMax && removedValue != value)
          UpdateCurrentMax();
      }
    }
  }
}