using System;
using UnityEngine;
using System.Collections.Generic;

namespace Mirror.Components.Experimental{
  /// <summary>
  /// Represents data sent from the client to track its current state.
  /// </summary>
  public struct ClientData{
    public int ClientNonce;
    public int ClientTick;
    public PacketLossTracker RemoteClientLoss;
    public int SentPackets;
  }

  /// <summary>
  /// Represents a server response that includes synchronization data for the client tick and packet loss information.
  /// </summary>
  public struct ServerPong{
    public ushort ServerTickWithNonce; // 5 bits for nonce + 11 bits for the tick
    public ushort ClientTickWithLoss; // 5 bits for packet loss value + 11 bits for the tick
  }

  /// <summary>
  /// Represents a server response with an absolute tick count to help the client synchronize with the server more accurately.
  /// </summary>
  public struct AbsoluteServerPong{
    public ushort ServerTickWithNonce; // 5 bits for nonce + 11 bits for the tick
    public ushort ClientTickWithLoss; // 5 bits for packet loss value + 11 bits for the tick
    public int AbsoluteServerTick; // Absolute server tick count to sync the client
  }

  /// <summary>
  /// Represents a client request sent to the server, including the client tick and a unique nonce for tracking.
  /// </summary>
  public struct ClientPing{
    public ushort ClientTickWithNonce; // 5 bits for nonce + 11 bits for the tick
  }

  // Ensure we run first and that there is only one instance present.
  [DefaultExecutionOrder(-10)]
  [DisallowMultipleComponent]
  [AddComponentMenu("Network/Network Tick Manager")]
  public class NetworkTickManager : NetworkBehaviour{
    /*** Public Definitions ***/

    #region Public Definitions

    [Header("Client Prediction Settings")]
    [Min(1)]
    [Tooltip("The minimum tick difference required between the client and server when the client's tick data arrives on the server." +
             "If the difference is less than this value, the client must adjust to stay synchronized.")]
    public int minClientRunaway = 1;

    [Min(1)]
    [Tooltip(
      "The allowable tick difference range added on top of the minimum client runaway." +
      "This defines how much further ahead the client can be from the server beyond the minimum before needing adjustment.")]
    public int acceptableClientRunawayRange = 1;

    [Header("Server State Replay Settings")]
    [Min(1)]
    [Tooltip(
      "The minimum tick difference required between the server and client when the client replays server states." +
      "If the difference is less than this value, the client must adjust the server replay tick.")]
    public int minServerRunaway = 0;

    [Min(1)]
    [Tooltip("The allowable tick difference range added on top of the minimum server runaway." +
             "Defines how much further ahead the received server state can be from the server replay beyond the minimum before needing adjustment.")]
    public int acceptableServerRunawayRange = 1;

    [Header("Deviation measurements and timings:")] [Tooltip("The amount of samples to use for packet loss calculation")] [Min(25)]
    public int packetLossSamples = 100;

    [Min(1)]
    [Tooltip("The duration in seconds over which deviation data is collected before adjusting the client or server states to acceptable runaway values.")]
    public int calculationSeconds = 2;

    [Min(10)]
    [Tooltip("The longer duration in seconds over which deviation data is collected before adjusting the client or server states to minimum runaway values.")]
    public int longCalculationSeconds = 30;

    [Tooltip("Tick compensation when packet loss is present: \ncompensation = loss percent / factor")] [Range(1, 30)]
    public int packetLossCompensationFactor = 10;

    [Header("Absolute tick sync settings:")]
    [Min(1)]
    [Tooltip("How often to verify absolute tick between the server and the client (this can happen if desync is larger than 1024 ticks)")]
    public int absoluteTickSyncIntervalSeconds = 10;

    [Min(10)] [Tooltip("How many ticks to send the absolute server tick for the clients to sync. Cant be 1 because this packet can get lost in transit.")]
    public int absoluteTickSyncHandshakeTicks = 10;

    [Header("Physics Controller:")] public NetworkPhysicsController physicsController;

    #endregion

    /*** Private Definitions ***/

    #region Private Definitions

    // Local clients list on the server for efficient communication
    private readonly Dictionary<NetworkConnection, ClientData> _clients = new Dictionary<NetworkConnection, ClientData>();

    // Instance of NetworkTick used for managing and changing the tick counters
    private readonly NetworkTick _networkTick = new NetworkTick();

    // Actual runaway metrics - adjusted based on network conditions ( aka packet losses )
    private int _internalClientRunaway = 0;
    private int _internalServerRunaway = 0;
    private int _internalMinClientRunaway = 0;

    private int _internalMinServerRunaway = 0;

    // Running minimum counters used to avoid adjusting too fast and oscillating back and forth
    private RunningMin _clientRunningMin;
    private RunningMin _serverRunningMin;
    private RunningMin _clientLongRunningMin;
    private RunningMin _serverLongRunningMin;

    // Client side packet loss tracker for packets from the server
    private PacketLossTracker _receivePacketLoss;

    // Server and Client last nonce values - used to dettect packet losses
    private int _serverNonce = 0;
    private int _clientNonce = 0;

    // Client side last client tick received from the server to avoid lengthy adjustments
    private int _lastRemoteClientTick = 0;

    // Absolute tick synch status
    private bool _isAbsoluteTickSynced = false;

    // Modulus ( % modulus == 0 -> send absolute tick to clients ) based on absoluteTickSyncIntervalSeconds and tick rate
    private int _absoluteServerTickModulus = 0;

    // Adjustment flags and variables used to ensure only one adjustment is taking place at a time.
    private bool _capturedOffsets = false;
    private bool _isAdjusting = false;
    private int _adjustmentEndTick = 0;

    // Make sure NetworkPhysicsController is attached to the tick manager
    protected new virtual void OnValidate() {
      base.OnValidate();
      physicsController = GetComponent<NetworkPhysicsController>();
      if (physicsController == null) {
        throw new ArgumentException("Missing NetworkPhysicsController! please attach it to this entity");
      }
    }

    public static NetworkTickManager singleton;
    
    void Awake() {
      if (singleton != null && singleton != this) {
        Destroy(gameObject);
        return;
      }
      singleton = this;
    }
    
    #endregion

    /*** Server Startup and Setup ***/

    #region Server Startup and Setup

    /// <summary>
    /// Called when the server starts. Registers callbacks for client connection and disconnection events,
    /// allowing the server to handle these events appropriately.
    /// </summary>
    [Server]
    public override void OnStartServer() {
      // Register callback for when clients connect/disconnect
      NetworkServer.OnConnectedEvent += OnClientConnected;
      NetworkServer.OnDisconnectedEvent += OnClientDisconnected;
      base.OnStartServer();
    }

    /// <summary>
    /// Called when the server stops. Unregisters callbacks for client connection and disconnection events
    /// to ensure cleanup and avoid unintended event handling after the server has stopped.
    /// </summary>
    [Server]
    public override void OnStopServer() {
      // Unregister callbacks when server stops
      NetworkServer.OnConnectedEvent -= OnClientConnected;
      NetworkServer.OnDisconnectedEvent -= OnClientDisconnected;
      base.OnStopServer();
    }

    /// <summary>
    /// Called when a client connects to the server. Initializes a new entry in the _clients dictionary for the connected client,
    /// storing initial client data such as nonce, tick count, packet loss tracker, and sent packets.
    /// </summary>
    /// <param name="conn">The network connection for the connected client.</param>
    [Server]
    private void OnClientConnected(NetworkConnection conn) => _clients[conn] = new ClientData()
      { ClientNonce = 0, ClientTick = 0, RemoteClientLoss = new PacketLossTracker(packetLossSamples), SentPackets = 0 };

    /// <summary>
    /// Called when a client disconnects from the server. Removes the client entry from the _clients dictionary
    /// to free up resources and maintain an accurate list of active clients.
    /// </summary>
    /// <param name="conn">The network connection for the disconnected client.</param>
    [Server]
    private void OnClientDisconnected(NetworkConnection conn) => _clients.Remove(conn);

    #endregion

    /*** Network Tick Start and Tick Initialization ***/

    #region Network Tick Start and Tick Initialization

    /// <summary> Initializes server-specific settings when the server starts. </summary>
    [Server]
    private void StartServer() {
      _absoluteServerTickModulus = Mathf.RoundToInt(absoluteTickSyncIntervalSeconds / Time.fixedDeltaTime);
      _networkTick.SetSynchronized(true);
      _networkTick.SetSynchronizing(false);
    }

    /// <summary> Initializes client-specific settings when the client starts. </summary>
    [Client]
    private void StartClient() {
      _internalMinClientRunaway = minClientRunaway;
      _internalMinServerRunaway = minServerRunaway;
      _internalClientRunaway = acceptableClientRunawayRange;
      _internalServerRunaway = acceptableServerRunawayRange;
      _clientRunningMin = new RunningMin(Mathf.RoundToInt(calculationSeconds / Time.fixedDeltaTime));
      _serverRunningMin = new RunningMin(Mathf.RoundToInt(calculationSeconds / Time.fixedDeltaTime));
      _clientLongRunningMin = new RunningMin(Mathf.RoundToInt(longCalculationSeconds / Time.fixedDeltaTime));
      _serverLongRunningMin = new RunningMin(Mathf.RoundToInt(longCalculationSeconds / Time.fixedDeltaTime));
      _receivePacketLoss = new PacketLossTracker(packetLossSamples);
    }

    /// <summary> Advances the client's tick counters by the specified number of ticks. </summary>
    /// <param name="deltaTicks">Number of ticks to advance.</param>
    [Client]
    private void OnTickForwardClient(int deltaTicks) {
      _networkTick.IncrementClientTick(deltaTicks);
      _networkTick.IncrementClientAbsoluteTick(deltaTicks);
      _networkTick.IncrementServerTick(deltaTicks);
      _networkTick.IncrementServerAbsoluteTick(deltaTicks);
    }

    /// <summary> Advances the server's tick counters by the specified number of ticks. </summary>
    /// <param name="deltaTicks">Number of ticks to advance.</param>
    [Server]
    private void OnTickForwardServer(int deltaTicks) {
      _networkTick.IncrementServerTick(deltaTicks);
      _networkTick.IncrementServerAbsoluteTick(deltaTicks);
    }

    /// <summary> Initializes the tick manager and sets up the physics controller based on whether it is running on the server or client. </summary>
    private void Start() {
      _networkTick.SetIsServer(isServer);
      if (isServer)
        StartServer();
      else
        StartClient();

      // Allow the physics to position all the items in the scene - we run 1 tick for this then wait for sync
      physicsController.TickForwardCallback = isServer ? OnTickForwardServer : OnTickForwardClient;
      physicsController.RunSimulate(1);
    }

    #endregion

    /*** Packet Loss Calculations ***/

    #region Packet Loss Calculations

    /// <summary> Updates the client's packet loss compensation values based on the server's nonce and reported packet loss. </summary>
    /// <param name="serverNonce">The nonce received from the server to detect packet loss.</param>
    /// <param name="sendPacketLoss">The packet loss percentage reported by the server.</param>
    [Client]
    private void UpdatePacketLossCompensation(int serverNonce, int sendPacketLoss) {
      _receivePacketLoss.AddPacket(_serverNonce > 0 && NextNonce(_serverNonce) != serverNonce);
      _serverNonce = serverNonce;

      // Calculate adjustments based on server and client packet loss factors
      var sendCompensationTicks = CalculateTickCompensation(sendPacketLoss);
      var receiveCompensationTicks = CalculateTickCompensation(_receivePacketLoss.Loss);

      // Update NetworkTick with the compensation values for users to integrate compensations if needed
      _networkTick.SetClientToServerPacketLossCompensation(sendCompensationTicks);
      _networkTick.SetServerToClientPacketLossCompensation(receiveCompensationTicks);

      // Adjust internal tick min and max runaway values to compensate for packet losses
      _internalMinClientRunaway = minClientRunaway + sendCompensationTicks;
      _internalClientRunaway = acceptableClientRunawayRange + _internalMinClientRunaway + sendCompensationTicks;
      _internalMinServerRunaway = minServerRunaway + receiveCompensationTicks;
      _internalServerRunaway = acceptableServerRunawayRange + _internalMinServerRunaway + receiveCompensationTicks;
    }

    #endregion

    /*** Synchronization functions ***/

    #region Synchronization functions

    /// <summary> Sets or adjusts the client's absolute server tick values to maintain synchronization with the server. </summary>
    /// <param name="absoluteServerTick">The absolute tick count provided by the server.</param>
    /// <param name="serverTick">The server's current tick value.</param>
    [Client]
    private void SetAbsoluteTicks(int absoluteServerTick, int serverTick) {
      if (!_isAbsoluteTickSynced) {
        _isAbsoluteTickSynced = true;
        _networkTick.SetServerTick(serverTick);
        _networkTick.SetServerAbsoluteTick(absoluteServerTick);
        return;
      }

      var proposedServerAbsoluteTick = absoluteServerTick - NetworkTick.SubtractTicks(serverTick, _networkTick.GetServerTick());
      var absoluteTickDiff = proposedServerAbsoluteTick - _networkTick.GetServerAbsoluteTick();
      if (absoluteTickDiff != 0) {
        _networkTick.IncrementServerAbsoluteTick(absoluteTickDiff);
        _networkTick.IncrementClientAbsoluteTick(absoluteTickDiff);
      }
    }

    /// <summary> Initiates the synchronization process by aligning the client's and server's ticks. </summary>
    /// <param name="serverTick">The current tick value from the server.</param>
    /// <param name="clientTick">The client's tick value as received by the server.</param>
    [Client]
    private void SynchronizeStart(int serverTick, int clientTick) {
      var oldServerTick = _networkTick.GetServerTick();
      _networkTick.IncrementServerTick(-_internalServerRunaway);
      var newServerTick = _networkTick.GetServerTick();
      _networkTick.IncrementServerAbsoluteTick(NetworkTick.SubtractTicks(newServerTick, oldServerTick));
      _networkTick.SetClientTick(NetworkTick.IncrementTick(serverTick,
        NetworkTick.SubtractTicks(_networkTick.GetClientTick(), clientTick) + _internalClientRunaway));
      _networkTick.SetClientAbsoluteTick(_networkTick.GetServerAbsoluteTick() + NetworkTick.SubtractTicks(_networkTick.GetClientTick(), newServerTick));
      _networkTick.SetSynchronizing(true);
      SetAdjusting(_networkTick.GetClientTick());
    }

    /// <summary> Synchronizes the client's and server's ticks by applying necessary adjustments. </summary>
    [Client]
    private void Synchronize() {
      var serverTickAdjustment = GetServerAdjustment(true);
      var clientTickAdjustment = GetServerAdjustment(true);
      // Apply adjustments on current tick counters
      _networkTick.IncrementClientTick(clientTickAdjustment);
      _networkTick.IncrementClientAbsoluteTick(clientTickAdjustment);
      _networkTick.IncrementServerTick(-serverTickAdjustment);
      _networkTick.IncrementServerAbsoluteTick(-serverTickAdjustment);
      // Set status to synchronized
      _networkTick.SetSynchronized(true);
      _networkTick.SetSynchronizing(false);

      SetAdjusting(_networkTick.GetClientTick());
    }

    #endregion

    /*** Handling Message from the Server ***/

    #region Handling Message from the Server

    /// <summary>
    /// Handles the server's pong response to maintain and adjust tick synchronization between the client and server.
    /// This method updates packet loss compensation, ensures packets are processed in order,
    /// and manages the client's synchronization state based on the received ticks.
    /// Depending on whether the client is synchronized, in the process of synchronizing, or not yet synchronized,
    /// it calculates necessary offsets and adjusts ticks to align with the server.
    /// </summary>
    /// <param name="serverNonce">Nonce value from the server for packet loss detection.</param>
    /// <param name="serverTick">Current tick count from the server.</param>
    /// <param name="sendLoss">Packet loss percentage reported by the server.</param>
    /// <param name="clientTick">Client's tick count as received by the server.</param>
    [Client]
    private void HandleServerPong(int serverNonce, int serverTick, int sendLoss, int clientTick) {
      _capturedOffsets = false;
      UpdatePacketLossCompensation(serverNonce, sendLoss);

      // We want to avoid handling the same client tick from the server to improve accuracy otherwise we risk of repeating adjustments
      if (!IsValidPacket(clientTick)) return;

      if (_networkTick.GetIsSynchronized()) {
        if (_isAdjusting && NetworkTick.SubtractTicks(clientTick, _adjustmentEndTick) > 0) {
          _isAdjusting = false;
          ResetRunningMins();
        }

        // Calculate and deviations using the server info
        CalculateOffsets(serverTick, clientTick);
        return;
      }

      if (_networkTick.GetIsSynchronizing()) {
        // Since client tick is not yet synchronized and the server sends old tick we need to compare to server tick rather than client tick
        if (NetworkTick.SubtractTicks(serverTick, _adjustmentEndTick) > 0) {
          // We are not worried about being too much ahead at this point, we only care about being behind the execution on client or server
          // So we calculate the minimum values and run the adjustment again
          CalculateOffsets(serverTick, clientTick);
          Synchronize();
          ResetRunningMins();
        }

        return;
      }

      // Wait until we receive positive tick from server before starting the initial 2 step sync  
      if (clientTick > 0) {
        SynchronizeStart(serverTick, clientTick);
        ResetRunningMins();
      }
    }

    #endregion

    /*** Tick Adjustment Calculations ***/

    #region Tick Adjustment Calculations

    /// <summary> Calculates the tick offsets between client and server, updating running minimums for synchronization adjustments. </summary>
    /// <param name="serverTick">The current tick value from the server.</param>
    /// <param name="clientTick">The client's tick value as received by the server.</param>
    [Client]
    private void CalculateOffsets(int serverTick, int clientTick) {
      _capturedOffsets = true;
      var clientTickOffset = NetworkTick.SubtractTicks(clientTick, serverTick);
      var serverTickOffset = NetworkTick.SubtractTicks(serverTick, _networkTick.GetServerTick());
      _clientRunningMin.Add(clientTickOffset);
      _clientLongRunningMin.Add(clientTickOffset);
      _serverRunningMin.Add(serverTickOffset);
      _serverLongRunningMin.Add(serverTickOffset);
    }

    /// <summary> Determines the necessary tick adjustment for the client to maintain synchronization with the server. </summary>
    /// <param name="absolute">If true, applies the full adjustment needed; otherwise, applies a minimal step.</param>
    /// <returns>The number of ticks to adjust the client's tick by.</returns>
    [Client]
    private int GetClientAdjustment(bool absolute = false) {
      // If the server received client predicted tick bellow min thresh hold we need to adjust ourselves forward otherwise risking server not receiving inputs
      if (_clientRunningMin.CurrentMin < _internalMinClientRunaway)
        return -(_clientRunningMin.CurrentMin - _internalMinClientRunaway);

      // If the server received client predicted tick is too far into the future we want to slow down the client to reduce perceived latency
      if (_clientRunningMin.IsFull && _clientRunningMin.CurrentMin > _internalClientRunaway)
        return absolute ? -_clientRunningMin.CurrentMin : -1;

      // If the server received client predicted tick is stable but above the min requirement we can slow down the client to reduce perceived latency
      if (_clientLongRunningMin.IsFull && _clientLongRunningMin.CurrentMin > _internalMinClientRunaway)
        return absolute ? -_clientLongRunningMin.CurrentMin : -1;
      return 0;
    }

    /// <summary> Determines the necessary tick adjustment for the server to maintain synchronization with the client. </summary>
    /// <param name="absolute">If true, applies the full adjustment needed; otherwise, applies a minimal step.</param>
    /// <returns>The number of ticks to adjust the server's tick by.</returns>
    [Client]
    private int GetServerAdjustment(bool absolute = false) {
      // If the received server tick is behind the expected minimum we need to adjust our tick backwards
      if (_serverRunningMin.CurrentMin < _internalMinServerRunaway)
        return _internalMinServerRunaway - _serverRunningMin.CurrentMin;

      // If the received server tick is too far forward we need to reduce it to reduce latency
      if (_serverRunningMin.IsFull && _serverRunningMin.CurrentMin > _internalServerRunaway)
        return absolute ? -_serverRunningMin.CurrentMin : -1;

      // If the received server tick is more than the minimum for an extended period of time its safe to reduce it to reduce latency
      if (_serverLongRunningMin.IsFull && _serverLongRunningMin.CurrentMin > _internalMinServerRunaway)
        return absolute ? -_serverLongRunningMin.CurrentMin : -1;
      return 0;
    }

    /// <summary> Calculates adjusted tick values for synchronization, applying any necessary client or server tick adjustments. </summary>
    /// <param name="deltaTicks">The base number of ticks to advance.</param>
    /// <returns>The adjusted number of ticks to use for simulation.</returns>
    [Client]
    private int GetAdjustedTicks(int deltaTicks) {
      int clientAdjustment = GetClientAdjustment();
      int serverAdjustment = GetServerAdjustment();
      if (serverAdjustment != 0) {
        _networkTick.IncrementServerTick(-serverAdjustment);
        _networkTick.IncrementServerAbsoluteTick(-serverAdjustment);
      }

      // If client or server are adjusting we need to wait for confirmation to avoid oscillating adjusments 
      if (clientAdjustment != 0 || serverAdjustment != 0)
        SetAdjusting(NetworkTick.IncrementTick(_networkTick.GetClientTick(), deltaTicks + clientAdjustment));

      return deltaTicks + clientAdjustment;
    }

    #endregion

    /*** Tick Simulation Functions ***/

    #region Tick Simulation Functions

    /// <summary> Checks for any required reconciliation due to state discrepancies and resimulates physics accordingly. </summary>
    /// <param name="deltaTicks">The number of ticks advanced since the last update.</param>
    [Client]
    private void CheckReconcile(int deltaTicks) {
      var reconcileStartTick = physicsController.GetReconcileStartTick();
      if (reconcileStartTick > 0) {
        var reconcileTicks = _networkTick.GetClientTick() - reconcileStartTick + deltaTicks;
        OnTickForwardClient(-reconcileTicks);
        _networkTick.SetReconciling(true);
        physicsController.RunSimulate(reconcileTicks);
        physicsController.ResetReconcile();
        _networkTick.SetReconciling(false);
      }
    }

    /// <summary> Updates the client's state each tick, handling synchronization, reconciliation, and physics simulation. </summary>
    /// <param name="deltaTicks">The number of ticks to advance.</param>
    [Client]
    private void UpdateClient(int deltaTicks) {
      // Check if need reconciling - if yes reconcile before executing the next ticks
      CheckReconcile(deltaTicks);

      // Adjust the delta ticks if not waiting for adjustment confirmation
      var adjustedTicks = _capturedOffsets && !_isAdjusting ? GetAdjustedTicks(deltaTicks) : deltaTicks;

      // fix discrepancies cause by client tick adjustment
      _networkTick.IncrementServerTick(deltaTicks - adjustedTicks);
      _networkTick.IncrementServerAbsoluteTick(deltaTicks - adjustedTicks);

      // Simulate ticks or skip if pause was requested
      if (adjustedTicks > 0)
        physicsController.RunSimulate(adjustedTicks);
    }

    /// <summary> Handles physics simulation and synchronization updates on both the server and client each fixed frame. </summary>
    public void FixedUpdate() {
      // Handle FixedUpdate for deltaTicks
      if (isServer) {
        physicsController.RunSimulate(1);
        SendUpdatesToAllClients();
      }
      else {
        // Keep pushing the tick counters forward until the client is synced with the server
        if (!_networkTick.GetIsSynchronized())
          OnTickForwardClient(1);
        else
          UpdateClient(1);
        ClientSendPing();
      }
    }

    #endregion

    /*** Communication Functions ***/

    #region Communication Functions

    /// <summary> Sends a ping to the server with the client's current tick and a nonce for packet loss detection. </summary>
    [Client]
    private void ClientSendPing() {
      // Increase nonce by 1 but keep withing 5 bits of data [0-31]
      _clientNonce = NextNonce(_clientNonce);
      CmdPingServer(new ClientPing() { ClientTickWithNonce = NetworkTick.CombineBitsTick(_clientNonce, _networkTick.GetClientTick()) });
    }

    /// <summary> Sends synchronization updates to all connected clients, including tick counts and packet loss information. </summary>
    [Server]
    private void SendUpdatesToAllClients() {
      // Increase nonce by 1 but keep withing 5 bits of data [0-31]
      _serverNonce = NextNonce(_serverNonce);
      var absoluteServerTick = _networkTick.GetServerAbsoluteTick();
      var isSendAbsolute = absoluteServerTick % _absoluteServerTickModulus == 0;
      var serverTickWithNonce = NetworkTick.CombineBitsTick(_serverNonce, _networkTick.GetServerTick());
      foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values) {
        // If connection is on the same machine as the server we skip it.
        if (conn == NetworkServer.localConnection) continue;

        if (_clients.TryGetValue(conn, out ClientData clientData)) {
          // 0-30 % are reported regularly but 31 or higher are aggregated as just 31 ( mor ethan 30% packet loss is extreme! )
          int compressedLoss = Math.Min(31, (int)Math.Ceiling(clientData.RemoteClientLoss.Loss));

          if (isSendAbsolute || clientData.SentPackets < absoluteTickSyncHandshakeTicks)
            // If requested by interval or during hand shake send absolute tick alongside tick information
            RpcAbsoluteServerPong(conn, new AbsoluteServerPong() {
              AbsoluteServerTick = absoluteServerTick,
              ServerTickWithNonce = serverTickWithNonce,
              ClientTickWithLoss = NetworkTick.CombineBitsTick(
                compressedLoss,
                clientData.ClientTick)
            });
          else
            // Send tick information with nonce and loss
            RpcServerPong(conn, new ServerPong() {
              ServerTickWithNonce = serverTickWithNonce,
              ClientTickWithLoss = NetworkTick.CombineBitsTick(
                compressedLoss,
                clientData.ClientTick)
            });

          // Count how many packets were sent
          clientData.SentPackets += 1;
          _clients[conn] = clientData;
        }
      }
    }

    #endregion

    /*** Target RPC and Command callbacks ***/

    #region Target RPC and Command callbacks

    /// <summary> Handles the server's response containing absolute tick synchronization data. </summary>
    /// <param name="target">The client connection receiving the response.</param>
    /// <param name="serverPong">The server's pong message with tick and nonce data.</param>
    [TargetRpc(channel = Channels.Unreliable)]
    private void RpcAbsoluteServerPong(NetworkConnectionToClient target, AbsoluteServerPong serverPong) {
      var (serverNonce, serverTick) = NetworkTick.SplitCombinedBitsTick(serverPong.ServerTickWithNonce);
      var (sendLoss, clientTick) = NetworkTick.SplitCombinedBitsTick(serverPong.ClientTickWithLoss);
      SetAbsoluteTicks(serverPong.AbsoluteServerTick, serverTick);
      HandleServerPong(serverNonce, serverTick, sendLoss, clientTick);
    }

    /// <summary> Handles the server's standard response containing synchronization data. </summary>
    /// <param name="target">The client connection receiving the response.</param>
    /// <param name="serverPong">The server's pong message with tick and nonce data.</param>
    [TargetRpc(channel = Channels.Unreliable)]
    private void RpcServerPong(NetworkConnectionToClient target, ServerPong serverPong) {
      var (serverNonce, serverTick) = NetworkTick.SplitCombinedBitsTick(serverPong.ServerTickWithNonce);
      var (sendLoss, clientTick) = NetworkTick.SplitCombinedBitsTick(serverPong.ClientTickWithLoss);
      HandleServerPong(serverNonce, serverTick, sendLoss, clientTick);
    }

    /// <summary> Receives ping messages from clients and updates their data on the server. </summary>
    /// <param name="clientPing">The ping message containing the client's tick and nonce.</param>
    /// <param name="connectionToClient">The connection to the client sending the ping.</param>
    [Command(requiresAuthority = false, channel = Channels.Unreliable)]
    private void CmdPingServer(ClientPing clientPing, NetworkConnectionToClient connectionToClient = null) {
      if (connectionToClient == null) return;
      var (nonce, clientTick) = NetworkTick.SplitCombinedBitsTick(clientPing.ClientTickWithNonce);
      _clients[connectionToClient].RemoteClientLoss.AddPacket(NextNonce(_clients[connectionToClient].ClientNonce) != nonce);
      _clients[connectionToClient] = new ClientData() {
        ClientTick = clientTick,
        ClientNonce = nonce,
        RemoteClientLoss = _clients[connectionToClient].RemoteClientLoss,
        SentPackets = _clients[connectionToClient].SentPackets,
      };
    }

    #endregion

    /*** Helper Functions ***/

    #region Helper Functions

    /// <summary> Validates whether the incoming packet is newer than the last processed one to prevent out-of-order processing. </summary>
    /// <param name="clientTick">The tick value from the incoming packet.</param>
    /// <returns>True if the packet is valid and should be processed; otherwise, false.</returns>
    [Client]
    private bool IsValidPacket(int clientTick) {
      var isValid = NetworkTick.SubtractTicks(clientTick, _lastRemoteClientTick) > 0;
      _lastRemoteClientTick = clientTick;
      return isValid;
    }

    /// <summary> Marks the start of an adjustment period, during which tick synchronization adjustments are applied. </summary>
    /// <param name="adjustmentEndTick">The client tick value when adjustments should stop.</param>
    [Client]
    private void SetAdjusting(int adjustmentEndTick) {
      _capturedOffsets = false;
      _isAdjusting = true;
      _adjustmentEndTick = adjustmentEndTick;
    }

    /// <summary> Resets the running minimums used for calculating tick adjustments, clearing any accumulated data. </summary>
    [Client]
    private void ResetRunningMins() {
      _capturedOffsets = false;
      _clientRunningMin.Reset();
      _clientLongRunningMin.Reset();
      _serverRunningMin.Reset();
      _serverLongRunningMin.Reset();
    }

    /// <summary>
    /// Generates the next nonce value by incrementing the current nonce. 
    /// The nonce is a looping 5-bit variable (0-31), ensuring it wraps around correctly when reaching 31.
    /// </summary>
    /// <param name="startNonce">The starting nonce value to increment.</param>
    /// <returns>The next nonce value, wrapped to stay within the 5-bit range.</returns>
    private static int NextNonce(int startNonce) => (startNonce + 1) & 0b11111;

    /// <summary>
    /// Calculates the tick compensation based on packet loss and a predefined compensation factor.
    /// This is used to adjust for lost packets, smoothing gameplay experience based on the 
    /// `packetLossCompensationFactor`.
    /// </summary>
    /// <param name="loss">The packet loss percentage used to calculate compensation ticks.</param>
    /// <returns>The number of ticks to compensate based on the provided packet loss.</returns>
    private int CalculateTickCompensation(float loss) => Mathf.FloorToInt((loss + packetLossCompensationFactor - 0.01f) / packetLossCompensationFactor);

    #endregion
  }
}