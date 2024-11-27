using System;

namespace Mirror{
  public class NetworkTick{
    /*** Private Definitions ***/

    #region Private Definitions

    // Current state flags
    private static bool _isServer = false;
    private static bool _isSynchronizing = false;
    private static bool _isSynchronized = false;
    private static bool _isReconciling = false;

    // Internal tick counters
    private static int _clientTick = 0;
    private static int _serverTick = 0;
    private static int _absoluteClientTick = 0;
    private static int _absoluteServerTick = 0;

    // Packet loss compensation ticks
    private static int _clientToServerPacketLossCompensation = 0;
    private static int _serverToClientPacketLossCompensation = 0;

    #endregion

    /*** CLIENT ONLY METHODS ***/

    #region CLIENT ONLY METHODS

    /// <summary> Gets the client-to-server packet loss compensation ticks. <para><b>Client-only:</b> This cant be accessed on the server.</para></summary>
    /// <exception cref="InvalidOperationException">Thrown if accessed on the server.</exception>
    public static int ClientToServerPacketLossCompensation {
      get {
        if (_isServer) throw new InvalidOperationException("ClientToServerPacketLossCompensation is client-only and cannot be accessed on the server.");
        return _clientToServerPacketLossCompensation;
      }
    }

    /// <summary> Gets the server-to-client packet loss compensation ticks. <para><b>Client-only:</b> This cant be accessed on the server.</para></summary>
    /// <exception cref="InvalidOperationException">Thrown if accessed on the server.</exception>
    public static int ServerToClientPacketLossCompensation {
      get {
        if (_isServer) throw new InvalidOperationException("ServerToClientPacketLossCompensation is client-only and cannot be accessed on the server.");
        return _serverToClientPacketLossCompensation;
      }
    }

    /// <summary>
    /// Sets the client-to-server packet loss compensation ticks, allowing the client to define the number of compensation ticks based on detected packet loss.
    /// <para><b>Client-only:</b> This method should not be called on the server.</para>
    /// </summary>
    /// <param name="compensationTicks">The number of compensation ticks to set.</param>
    public void SetClientToServerPacketLossCompensation(int compensationTicks) {
      if (_isServer) throw new InvalidOperationException("SetClientToServerPacketLossCompensation is client-only and cannot be accessed on the server.");
      _clientToServerPacketLossCompensation = compensationTicks;
    }

    /// <summary>
    /// Sets the server-to-client packet loss compensation ticks, allowing the client to define the number of compensation ticks based on detected packet loss.
    /// <para><b>Client-only:</b> This method should not be called on the server.</para>
    /// </summary>
    /// <param name="compensationTicks">The number of compensation ticks to set.</param>
    public void SetServerToClientPacketLossCompensation(int compensationTicks) {
      if (_isServer) throw new InvalidOperationException("SetServerToClientPacketLossCompensation  is client-only and cannot be accessed on the server.");
      _serverToClientPacketLossCompensation = compensationTicks;
    }

    #endregion

    /*** Static Status Getters ***/

    #region Static Status Getters

    /// <summary> Gets a value indicating whether the current instance is a server. </summary>
    public static bool IsServer => _isServer;

    /// <summary> Gets a value indicating whether the client is synchronizing with the server. </summary>
    public static bool IsSynchronizing => _isSynchronizing;

    /// <summary> Gets a value indicating whether the client is synchronized with the server. </summary>
    public static bool IsSynchronized => _isSynchronized;

    /// <summary> Gets a value indicating whether the system is reconciling ticks. </summary>
    public static bool IsReconciling => _isReconciling;

    #endregion

    /*** Static Tick Getters ***/

    #region Static Tick Getters

    /// <summary> Gets the current tick count based on whether the instance is a server or client. </summary>
    public static int CurrentTick => _isServer ? _serverTick : _clientTick;

    /// <summary> Gets the current absolute tick count based on whether the instance is a server or client. </summary>
    public static int CurrentAbsoluteTick => _isServer ? _absoluteServerTick : _absoluteClientTick;

    /// <summary> Gets the client tick count. </summary>
    public static int ClientTick => _clientTick;

    /// <summary> Gets the client absolute tick count. </summary>
    public static int ClientAbsoluteTick => _absoluteClientTick;

    /// <summary> Gets the server tick count. </summary>
    public static int ServerTick => _serverTick;

    /// <summary> Gets the server tick count. </summary>
    public static int ServerAbsoluteTick => _absoluteServerTick;

    #endregion

    /*** Instance Getters ***/

    #region Instance Getters

    /// <summary> Checks if the client is in the process of synchronizing with the server. </summary>
    public bool GetIsSynchronizing() => _isSynchronizing;

    /// <summary> Checks if the client is currently synchronized with the server. </summary>
    public bool GetIsSynchronized() => _isSynchronized;

    /// <summary>Gets the current client tick value.</summary>
    public int GetClientTick() => _clientTick;

    /// <summary>Gets the absolute tick value for the client.</summary>
    public int GetClientAbsoluteTick() => _absoluteClientTick;

    /// <summary>Gets the current client tick value.</summary>
    public int GetServerTick() => _serverTick;

    /// <summary>Gets the absolute tick value for the server.</summary>
    public int GetServerAbsoluteTick() => _absoluteServerTick;

    #endregion

    /*** Instance Status Setters ***/

    #region Instance Status Setters

    /// <summary> Sets the server status of the current instance. </summary>
    public void SetIsServer(bool isServer) => _isServer = isServer;

    /// <summary> Sets the synchronization status between client and server. </summary>
    public void SetSynchronized(bool isSynchronized) => _isSynchronized = isSynchronized;

    /// <summary> Sets the synchronization status between client and server. </summary>
    public void SetSynchronizing(bool isSynchronizing) => _isSynchronizing = isSynchronizing;

    /// <summary> Sets the reconciling status. </summary>
    public void SetReconciling(bool reconciling) => _isReconciling = reconciling;

    #endregion

    /*** Instance Tick Setters ***/

    #region Instance Tick Setters

    /// <summary>Sets a new tick value for the client.</summary>
    public void SetClientTick(int newTick) => _clientTick = newTick;

    /// <summary>Sets a new absolute tick value for the client.</summary>
    public void SetClientAbsoluteTick(int newAbsoluteTick) => _absoluteClientTick = newAbsoluteTick;

    /// <summary>Sets a new tick value for the server.</summary>
    public void SetServerTick(int newTick) => _serverTick = newTick;

    /// <summary>Sets a new absolute tick value for the server.</summary>
    public void SetServerAbsoluteTick(int newAbsoluteTick) => _absoluteServerTick = newAbsoluteTick;

    #endregion

    /*** Instance Tick Modifiers ***/

    #region Instance Tick Modifiers

    /// <summary>Increments the client tick by a specified amount, wrapping to 11 bits.</summary>
    public void IncrementClientTick(int increment) => _clientTick = (_clientTick + increment) & 0b11111111111;

    /// <summary>Increments the client's absolute tick by a specified amount.</summary>
    public void IncrementClientAbsoluteTick(int increment) => _absoluteClientTick += increment;

    /// <summary>Increments the server tick by a specified amount, wrapping to 11 bits.</summary>
    public void IncrementServerTick(int increment) => _serverTick = (_serverTick + increment) & 0b11111111111;

    /// <summary>Increments the server's absolute tick by a specified amount.</summary>
    public void IncrementServerAbsoluteTick(int increment) => _absoluteServerTick += increment;

    #endregion

    /*** Useful Bitwise Functions ***/

    #region Useful Bitwise Functions

    /// <summary>
    /// Combines a fiveBits and tick counter into a single <see cref="ushort"/> value. This is used to optimize network traffic by packing two values into one.
    /// </summary>
    /// <param name="fiveBits">The fiveBits value (should be within 5 bits).</param>
    /// <param name="tick">The tick counter value (should be within 11 bits).</param>
    /// <returns>A combined <see cref="ushort"/> containing both the fiveBits and tick counter.</returns>
    public static ushort CombineBitsTick(int fiveBits, int tick) {
      // Ensure the fiveBits is within 5 bits and tickCounter within 11 bits
      fiveBits &= 0x1F; // Mask to keep only the lowest 5 bits
      tick &= 0x7FF; // Mask to keep only the lowest 11 bits
      return (ushort)((fiveBits << 11) | tick); // Shift fiveBits left by 11 bits and combine with tickCounter
    }

    /// <summary>
    /// Splits a combined fiveBits and tick counter value back into its individual components.
    /// </summary>
    /// <param name="combined">The combined <see cref="ushort"/> value.</param>
    /// <returns>A tuple containing the fiveBits and tick counter.</returns>
    public static (int fiveBits, int tickCounter) SplitCombinedBitsTick(ushort combined) {
      var fiveBits = (combined >> 11) & 0x1F; // Extract the 5-bit fiveBits by shifting right and masking
      var tickCounter = combined & 0x7FF; // Extract the 11-bit tick counter by masking the lower 11 bits
      return (fiveBits, tickCounter);
    }

    /// <summary>
    /// Calculates the minimal difference between two ticks, accounting for wraparound (ex: SubtractTicks(2040, 2) => 10).
    /// This helps in correctly comparing tick counts in a circular tick range.
    /// </summary>
    /// <param name="tickOne">The first tick value.</param>
    /// <param name="tickTwo">The second tick value.</param>
    /// <returns>The minimal difference between the two ticks.</returns>
    public static int SubtractTicks(int tickOne, int tickTwo) {
      var delta = (tickOne - tickTwo + 2048) % 2048;
      if (delta >= 1024) delta -= 2048;
      return delta;
    }

    /// <summary>
    /// Increments a tick value by a specified amount, wrapping around within a 2048 tick range.
    /// This function ensures that tick values stay within a defined range by handling wraparound correctly.
    /// </summary>
    /// <param name="tick">The initial tick value.</param>
    /// <param name="increment">The amount to increment the tick by.</param>
    /// <returns>The incremented tick value, wrapped within the 2048 range.</returns>
    public static int IncrementTick(int tick, int increment) {
      return (tick + increment) & 0b11111111111;
    }

    #endregion
  }
}