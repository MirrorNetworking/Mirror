using System;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;

#nullable enable
namespace Mirror.Components.Experimental{
  public abstract class NetworkPlayerControllerBase : NetworkBehaviour, INetworkedItem{
    private const int HistoryBufferSize = 2048; // Equal to tick rollover counter for ease of use (no modulus needed)

    /* Public variables and settings */

    #region Public variables and settings

    // Functions for comparing additional states and inputs for network optimization.
    // Each function accepts two byte arrays:
    // - For change compare functions: the first is the previous additional state/inputs, the second is the new additional state/inputs.
    // Returns a byte array representing the differences; an empty array indicates no changes.
    public Func<byte[], byte[], byte[]>? AdditionalInputsChangeCompare;
    public Func<byte[], byte[], byte[]>? AdditionalStateChangeCompare;

    // Functions for overriding additional states and inputs for network optimization.
    // Each function accepts two byte arrays:
    // - For override functions: the first is the base additional state/inputs, the second is the override state/input.
    // Returns a byte array representing the additional state/inputs after the modifications.
    public Func<byte[], byte[], byte[]>? AdditionalInputsOverride;
    public Func<byte[], byte[], byte[]>? AdditionalStateOverride;

    [Header("Physics Execution Settings")] [Tooltip("Affects when the character is executed during physics tick (higher = earlier)")]
    public int executionPriority = 1000;

    [Header("Compensation options")]
    [Min(1)]
    [Tooltip("Minimum past ticks data to attach to current tick data when packet loss is detected.\n\n" +
             "Note: With higher packet loss, the compensation increases on its own.")]
    public int minCompensationTicks = 1;

    [Header("Inputs synchronization Options")]
    [Min(1)]
    [Tooltip("Defines how often to send a full input set to ensure accurate compensation and prevent desynchronization.")]
    public int inputsSyncModulus = 24;

    [Tooltip("Will send current and previous tick inputs to avoid desync on single packet loss.")]
    public bool durableInputs = false;

    [Header("State synchronization Options")] [Min(1)] [Tooltip("Defines how often to send absolute state updates.")]
    public int stateSyncModulus = 24;

    [Tooltip("Whether to send state changes.")]
    public bool sendStateChanges = true;

    [Tooltip("Will send current and previous tick states to avoid desync on single packet loss.")]
    public bool durableStates = false;

    [Header("Reconciliation Options")] [SerializeField, Tooltip("Reconcile when position on the server does not match local position history.")]
    internal SyncInterpolateOptions reconcileBy = SyncInterpolateOptions.Position | SyncInterpolateOptions.Rotation | SyncInterpolateOptions.Velocity;

    [Min(0)] [Tooltip("Starts reconciliation this many ticks before desync detection. Prevents failures caused by stale component state from previous ticks.")]
    public int extraReconcileTicks = 0;

    [Tooltip("Adjusts player state to correct minor desync errors without triggering full reconciliation, preventing cumulative error build-up.")]
    public bool softMicroAdjustments = true;

    [Header("Reconciliation Debug Options")] [Tooltip("Show reconcile events in console.")]
    public bool showReconciliationLog = true;

    [Tooltip("Show soft reconcile events in console.")]
    public bool showSoftReconciliationLog = true;

    #endregion

    /* Private variables */

    #region Private variables

    // (Server) Set of client connection IDs when set client will receive normal data otherwise sync data
    private readonly HashSet<int> _syncedClients = new HashSet<int>();

    // (Server/Client) Flag signaling that the player is ready to execute and send inputs
    private bool _isPlayerReady = false;

    // (Client) Flag set by OnResetNetworkState and cleared at tick end that signals first tick of reconciliation
    private bool _isPendingStateReset = false;

    // (Server/Client) The tick at which local player synchronization ends
    private int _localPlayerSyncEnd = 0;

    // (Server/Client) The tick at which PlayerStart() will be executed
    private int _playerStartTick = int.MaxValue;

    // (Server/Client) For local players: prevents reconcile checks; for remote players: prevents snapping to server position
    private bool _isSimulating = false;

    // Tick after which simulation ends (for local players, the next tick; for remote players, when server tick equals client tick)
    private int _simulationEndTick = 0;

    // Tick after which soft reconciliation adjustments can resume
    private int _softReconcileLock = 0;

    // (Server) Tick at which to force a full state sync regardless of modulus condition
    private int _forceStateSyncTick = 0;

    // Inputs and states of the player recorded locally
    private readonly NetworkPlayerInputs[] _playerInputsHistory = new NetworkPlayerInputs[HistoryBufferSize];
    private readonly NetworkPlayerState[] _playerStateHistory = new NetworkPlayerState[HistoryBufferSize];

    // Inputs and states of the received player data from client to server and server to client
    private readonly NetworkPlayerInputs[] _receivedPlayerInputs = new NetworkPlayerInputs[HistoryBufferSize];
    private readonly NetworkPlayerState[] _receivedPlayerStates = new NetworkPlayerState[HistoryBufferSize];

    // Queued inputs and state data to be sent between server and client
    private readonly NetworkPlayerInputs[] _inputsSendQueue = new NetworkPlayerInputs[HistoryBufferSize];
    private readonly NetworkPlayerState[] _stateSendQueue = new NetworkPlayerState[HistoryBufferSize];

    // Whether position desynchronization will trigger a reconcile event
    private bool _reconcileByPosition => (reconcileBy & SyncInterpolateOptions.Position) == SyncInterpolateOptions.Position;

    // Whether rotation desynchronization will trigger a reconcile event
    private bool _reconcileByRotation => (reconcileBy & SyncInterpolateOptions.Rotation) == SyncInterpolateOptions.Rotation;

    // Whether velocity desynchronization will trigger a reconcile event
    private bool _reconcileByVelocity => (reconcileBy & SyncInterpolateOptions.Velocity) == SyncInterpolateOptions.Velocity;

    [Flags]
    public enum SyncInterpolateOptions{
      Position = 1 << 0,
      Rotation = 1 << 1,
      Velocity = 1 << 2,
    }

    #endregion

    /* Network Item Registration */

    #region Network Item Registration

    /// <summary>
    /// Registers the network entity in the <see cref="NetworkPhysicsEntity"/> system when the object is enabled,
    /// ensuring it participates in network physics updates.
    /// </summary>
    protected virtual void OnEnable() => NetworkPhysicsEntity.AddNetworkEntity(this, executionPriority);

    /// <summary>
    /// Unregisters the network entity from the <see cref="NetworkPhysicsEntity"/> system when the object is disabled,
    /// cleaning up resources and preventing unnecessary updates.
    /// </summary>
    protected virtual void OnDisable() => NetworkPhysicsEntity.RemoveNetworkEntity(this);

    /// <summary>
    /// Unregisters the network entity from the <see cref="NetworkPhysicsEntity"/> system when the object is destroyed,
    /// cleaning up resources and preventing unnecessary updates.
    /// </summary>
    protected virtual void OnDestroy() => NetworkPhysicsEntity.RemoveNetworkEntity(this);

    #endregion

    /* Required methods */

    #region Required methods

    /* Inputs */

    /// <summary> Retrieves the current player inputs. </summary>
    public abstract NetworkPlayerInputs GetPlayerInputs();

    /// <summary> Sets the current player inputs. </summary>
    public abstract void SetPlayerInputs(NetworkPlayerInputs inputs);

    /// <summary> Resets the player inputs at the first tick of reconciliation. Called at first reconcile tick. </summary>
    public abstract void ResetPlayerInputs(NetworkPlayerInputs inputs);

    /* States */

    /// <summary> Retrieves the current player state. </summary>
    public abstract NetworkPlayerState GetPlayerState();

    /// <summary> Applies the specified player state. </summary>
    public abstract void SetPlayerState(NetworkPlayerState state);

    /// <summary> Resets the player state at the first tick of reconciliation. Called at first reconcile tick. </summary>
    public abstract void ResetPlayerState(NetworkPlayerState state);

    #endregion

    /* Overridable methods */

    #region Overridable methods

    /// <summary> Called when the player takes control of the character, either locally or remotely. </summary>
    public virtual void PlayerStart() {
    }

    /// <summary> Called during reconciliation checks to allow implementing custom logic. Return true if reconciliation should be triggered. </summary>
    protected virtual bool CustomReconcileCheck(NetworkPlayerState localState, NetworkPlayerState remoteState) {
      // Overridable method to allow for custom reconcile requests
      return false;
    }

    /// <summary> Called to reset network state; override to implement custom reset logic. </summary>
    public virtual void ResetNetworkState() {
    }

    /// <summary> Called before reconciliation begins. </summary>
    public virtual void BeforeNetworkReconcile() {
    }

    /// <summary> Called after reconciliation has concluded. </summary>
    public void AfterNetworkReconcile() {
    }

    /// <summary> Called before the network update begins. </summary>
    public virtual void BeforeNetworkUpdate(int deltaTicks, float deltaTime) {
    }

    /// <summary> Called before a network tick is executed, similar to Update. </summary>
    public virtual void NetworkUpdate(int deltaTicks, float deltaTime) {
    }

    /// <summary> Called after a network tick has been executed. </summary>
    public virtual void AfterNetworkUpdate(int deltaTicks, float deltaTime) {
    }

    #endregion

    /* Utility Methods */

    #region Utility Methods

    /// <summary> Forces a full state synchronization on the next update cycle. </summary>
    [Server]
    protected void ForceStateSync() => _forceStateSyncTick = NetworkTick.ServerAbsoluteTick;

    /// <summary>
    /// Forces the local player into simulation for the next client tick,
    /// temporarily disabling reconciliation checks.
    /// Necessary when colliding with or affecting server-controlled objects.
    /// </summary>
    [Client]
    protected void SimulateNextTick() {
      _isSimulating = true;
      _simulationEndTick = NetworkTick.IncrementTick(NetworkTick.ClientTick, 1);
    }

    /// <summary> Indicates whether the player is synchronized and ready (replaces Start()). </summary>
    protected bool IsPlayerReady => _isPlayerReady;

    /// <summary> Indicates whether the player is running a local simulation or is synchronized with the server. </summary>
    protected bool IsSimulating => _isSimulating;

    #endregion

    /* Player Synchronization methods */

    #region Player Synchronization methods

    /// <summary> Ensures that PlayerStart is called even after client synchronization is complete. </summary>
    public override void OnStartClient() {
      if (isServer) return; // ignore client on the hosted server

      // Either the server or the player signaled to create a player; in either case, reconciliation is not needed.
      if (NetworkTick.IsSynchronized && isLocalPlayer)
        _playerStartTick = NetworkTick.IncrementTick(NetworkTick.ClientTick, 1);
    }

    /// <summary>
    /// Synchronizes the client with the server:
    /// for the local player, duplicates server inputs and sets a sync endpoint;
    /// for remote players, notifies the server and sets the start tick for accurate reconciliation.
    /// </summary>
    public void OnNetworkSynchronized() {
      // Server has network sync at the beginning so we skip this for the host.
      if (isServer) _playerStartTick = NetworkTick.IncrementTick(NetworkTick.CurrentTick, 1);
      // If not server and not local player, send a reliable end sync request.
      else if (!isLocalPlayer) ClientSynchronizedCmd();
      else {
        // For the local player, mark the current client tick as the sync endpoint.
        // This ensures inputs are sent until the server tick reaches or exceeds this value and set waiting for network start
        _localPlayerSyncEnd = NetworkTick.ClientAbsoluteTick;
        _playerStartTick = NetworkTick.IncrementTick(NetworkTick.CurrentTick, 1);
      }
    }

    /// <summary> Checks if network start conditions are met and initiates PlayerStart(). </summary>
    private void CheckForPlayerStart() {
      var compareTick = isLocalPlayer ? NetworkTick.CurrentTick : NetworkTick.ServerTick;

      // If the server and clients are in sync, fire network start.
      if (_playerStartTick != int.MaxValue && NetworkTick.SubtractTicks(compareTick, _playerStartTick) >= 0) {
        _isPlayerReady = true;
        PlayerStart();
      }
    }

    /// <summary> Sets the remote player's start tick using the earliest valid tick found in the input list. </summary>
    private void SetRemotePlayerStart(List<NetworkPlayerInputs> inputsList) {
      // Find the earliest tick that has a tick number attached.
      foreach (var inputs in inputsList)
        // Ensure the data actually contains valid ticks.
        if (inputs.TickNumber.HasValue) {
          var tick = inputs.TickNumber.Value;
          if (_playerStartTick == int.MaxValue || NetworkTick.SubtractTicks(tick, _playerStartTick) < 0) _playerStartTick = tick;
          // Only care about the earliest tick.
          break;
        }
    }

    #endregion

    /* Tick simulation and update handling  */

    #region Tick simulation and update handling

    /// <summary>
    /// Server-side update method that records the player's state and input history for the current tick.
    /// It retrieves the current server tick, saves the player's state at that tick, and if the player is ready,
    /// stores local input data directly or overlays remote inputs onto the previous tick's inputs.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks elapsed since the last update.</param>
    /// <param name="deltaTime">The time in seconds elapsed since the last update.</param>
    [Server]
    private void OnServerPlayerUpdate(int deltaTicks, float deltaTime) {
      var serverTick = NetworkTick.ServerTick;

      // Record player state in history.
      _playerStateHistory[serverTick] = GetPlayerStateWithTick(serverTick);

      if (_isPlayerReady) {
        // For the local player, simply save history to send to remote clients.
        if (isLocalPlayer) _playerInputsHistory[serverTick] = GetPlayerInputsWithTick(serverTick);
        // For remote players, overlay received changes on the previous inputs.
        else {
          var previousTick = NetworkTick.IncrementTick(serverTick, -1);
          _playerInputsHistory[serverTick] = _playerInputsHistory[previousTick]
            .OverrideInputsWith(_receivedPlayerInputs[serverTick], serverTick, AdditionalInputsOverride);
        }
      }
    }

    /// <summary> Handles the local player's update on the client side each tick. </summary>
    /// <param name="deltaTicks">The number of ticks elapsed since the last update.</param>
    /// <param name="deltaTime">The time in seconds elapsed since the last update.</param>
    /// <remarks>
    /// - If the client is in reconciliation mode, it rebuilds the received data using the latest server state.
    /// - If there is no pending state reset, it updates the player state history by overlaying the received state.
    /// - When the player is ready and not reconciling, it records the current player inputs, queues them for sending,
    ///   sends them to the server with packet loss compensation, and checks if a reconciliation is required.
    /// </remarks>
    [Client]
    private void OnClientLocalPlayerUpdate(int deltaTicks, float deltaTime) {
      var currentTick = NetworkTick.CurrentTick;

      // If reconciling, rebuild data using the most recent server state.
      if (NetworkTick.IsReconciling) RebuildReceivedData(currentTick);

      // Record player state in history if not pending reset.
      if (!_isPendingStateReset)
        _playerStateHistory[currentTick] = GetPlayerStateWithTick(currentTick)
          .OverrideStateWith(_receivedPlayerStates[currentTick], currentTick, AdditionalStateOverride);

      if (_isPlayerReady && !NetworkTick.IsReconciling) {
        _playerInputsHistory[currentTick] = GetPlayerInputsWithTick(currentTick);
        AddPlayerInputsToSendQueue();
        SmartSendInputsToServer(NetworkTick.ServerAbsoluteTick <= _localPlayerSyncEnd, NetworkTick.ClientToServerPacketLossCompensation);
        CheckIfReconcile();
      }
    }

    /// <summary>
    /// Updates the remote player's input and state history on the client. 
    /// If not in a state reset, it updates the state history using received data—allowing deviation if simulating—and synchronizes input history.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks elapsed since the last update.</param>
    /// <param name="deltaTime">The time in seconds elapsed since the last update.</param>
    [Client]
    private void OnClientRemotePlayerUpdate(int deltaTicks, float deltaTime) {
      var serverTick = NetworkTick.ServerTick;

      // Update history states with received inputs unless during reset phase.
      if (!_isPendingStateReset)
        // If simulating we want to let the character deviate rather than snap it to server position.
        _playerStateHistory[serverTick] = _isSimulating
          ? GetPlayerStateWithTick(serverTick) // If simulating, allow deviation.
          : GetPlayerStateWithTick(serverTick).OverrideStateWith(_receivedPlayerStates[serverTick], serverTick, AdditionalStateOverride);

      // Update history inputs with received inputs.
      _playerInputsHistory[serverTick] = _receivedPlayerInputs[serverTick];
    }

    /// <summary>
    /// Applies the stored player state and inputs for the current tick. 
    /// If a network state reset is pending, resets state and inputs; otherwise, sets them normally.
    /// </summary>
    private void ApplyStatesAndInputs() {
      var targetTick = isLocalPlayer ? NetworkTick.CurrentTick : NetworkTick.ServerTick;

      if (_isPendingStateReset) {
        ResetPlayerState(_playerStateHistory[targetTick]);
        if (_isPlayerReady) ResetPlayerInputs(_playerInputsHistory[targetTick]);
      }
      else {
        SetPlayerState(_playerStateHistory[targetTick]);
        if (_isPlayerReady) SetPlayerInputs(_playerInputsHistory[targetTick]);
      }
    }

    /// <summary>
    /// Updates player state depending on whether this instance is server or client, and whether the player is local or remote.
    /// Then handles sending or receiving data.
    /// </summary>
    public void OnBeforeNetworkUpdate(int deltaTicks, float deltaTime) {
      if (!_isPlayerReady) CheckForPlayerStart();

      if (_isPendingStateReset) BeforeNetworkReconcile();
      else BeforeNetworkUpdate(deltaTicks, deltaTime);

      if (isServer) OnServerPlayerUpdate(deltaTicks, deltaTime);
      else {
        RebuildReceivedData(NetworkTick.ServerTick);
        if (isLocalPlayer) OnClientLocalPlayerUpdate(deltaTicks, deltaTime);
        else OnClientRemotePlayerUpdate(deltaTicks, deltaTime);
      }

      ApplyStatesAndInputs();
    }

    /// <summary>
    /// Executes the network update cycle: runs custom logic, sends data to clients on the server,
    /// and checks for simulation end on the client.
    /// </summary>
    /// <param name="deltaTicks">Elapsed ticks since the last update.</param>
    /// <param name="deltaTime">Elapsed time in seconds since the last update.</param>
    public void OnNetworkUpdate(int deltaTicks, float deltaTime) {
      // Call custom player logic before sending data.
      NetworkUpdate(deltaTicks, deltaTime);

      // Server sends data to all connected clients.
      if (isServer) SendDataToClients();

      // Check if simulation should end on the client.
      else CheckSimulationEnd();
    }

    /// <summary> Executes post-update logic and clears outdated tick data. </summary>
    /// <param name="deltaTicks">Number of ticks advanced since the last update.</param>
    /// <param name="deltaTime">Time elapsed since the last update.</param>
    public void OnAfterNetworkUpdate(int deltaTicks, float deltaTime) {
      AfterNetworkUpdate(deltaTicks, deltaTime);
      ClearPastTickData();
    }

    #endregion

    /* Reconciliation checks and logic */

    #region Reconciliation checks and logic

    /// <summary>
    /// Rebuilds the received input and state data for the specified tick by overlaying the current tick's values
    /// on the previous tick's data. If state changes are enabled and valid, the state is updated similarly.
    /// </summary>
    private void RebuildReceivedData(int rebuildTick) {
      var previousServerTick = NetworkTick.IncrementTick(rebuildTick, -1);

      // Assume no input changes if not sent; overlay current inputs on previous tick.
      _receivedPlayerInputs[rebuildTick] = _receivedPlayerInputs[previousServerTick]
        .OverrideInputsWith(_receivedPlayerInputs[rebuildTick], rebuildTick, AdditionalInputsOverride);

      // If sending precise server changes, overlay state changes.
      if (sendStateChanges && _receivedPlayerStates[rebuildTick].HasTick)
        _receivedPlayerStates[rebuildTick] = _receivedPlayerStates[previousServerTick]
          .OverrideStateWith(_receivedPlayerStates[rebuildTick], rebuildTick, AdditionalStateOverride);
    }

    /// <summary>
    /// Client-only method that resets the player’s network state at the specified tick by overriding input/state with received data,
    /// then triggers reconciliation.
    /// </summary>
    [Client]
    public void OnResetNetworkState() {
      var targetTick = isLocalPlayer ? NetworkTick.CurrentTick : NetworkTick.ServerTick;

      // Override inputs and states with the most updated received data.
      _playerInputsHistory[targetTick] = _playerInputsHistory[targetTick]
        .OverrideInputsWith(_receivedPlayerInputs[targetTick], targetTick, AdditionalInputsOverride);
      _playerStateHistory[targetTick] =
        _playerStateHistory[targetTick].OverrideStateWith(_receivedPlayerStates[targetTick], targetTick, AdditionalStateOverride);

      // Call virtual BeforeReconcile function to allow developers to implement custom logic like storing position for smoothing desync
      ResetNetworkState();

      // Set pending reset flag to suspend state collection and apply reset methods.
      _isPendingStateReset = true;
    }

    /// <summary>
    /// Client-only method that compares the latest local and remote player states.
    /// If significant discrepancies are found, queues a full reconcile request; otherwise, applies a soft adjustment.
    /// </summary>
    [Client]
    private void CheckIfReconcile() {
      var targetTick = NetworkTick.ServerTick;
      var remoteState = _receivedPlayerStates[targetTick];
      var localState = _playerStateHistory[targetTick];
      // Avoid reconcile check when already reconciling, simulating, or lacking valid data.
      if (_isSimulating || !_receivedPlayerStates[targetTick].HasTick || !_playerStateHistory[targetTick].HasTick) return;

      // we dont use the custom compare functions we only care about position, rotation and velocity
      var changes = _playerStateHistory[targetTick].GetChangedStateComparedTo(_receivedPlayerStates[targetTick]);
      if (changes.HasValue && (
            (_reconcileByPosition && changes.Value.Position.HasValue) ||
            (_reconcileByRotation && changes.Value.Rotation.HasValue) ||
            (_reconcileByVelocity && changes.Value.BaseVelocity.HasValue) ||
            CustomReconcileCheck(localState, localState.OverrideStateWith(remoteState, targetTick, AdditionalStateOverride))
          )) {
        // Request reconcile from the faulty tick (including extra ticks for compensation).
        NetworkPhysicsController.RequestReconcileFromTick(NetworkTick.IncrementTick(targetTick, -extraReconcileTicks));
        // Log reconcile debug information if enabled.
        if (showReconciliationLog) {
          var reconcileSources = new List<string>();
          if (_reconcileByPosition && changes.Value.Position.HasValue) reconcileSources.Add("Position");
          if (_reconcileByRotation && changes.Value.Rotation.HasValue) reconcileSources.Add("Rotation");
          if (_reconcileByVelocity && changes.Value.BaseVelocity.HasValue) reconcileSources.Add("Velocity");
          Debug.Log("Reconcile from tick " + targetTick + " because (" + string.Join(", ", reconcileSources) + ")");
        }
      }
      else if (softMicroAdjustments && _softReconcileLock < NetworkTick.ServerAbsoluteTick) {
        // Check for any state differences, even if minor.
        // we dont use the custom compare functions we only care about position, rotation and velocity
        changes = _playerStateHistory[targetTick].GetChangedStateComparedTo(_receivedPlayerStates[targetTick], true);
        if (changes.HasValue && (changes.Value.Position.HasValue || changes.Value.BaseVelocity.HasValue || changes.Value.Rotation.HasValue)) {
          // Wait until after the updated tick to avoid compounding errors.
          _softReconcileLock = NetworkTick.ClientAbsoluteTick;

          // Calculate deviations for adjustment.
          Vector3? positionDeviation = changes.Value.Position.HasValue ? remoteState.Position - localState.Position : null;
          Vector3? velocityDeviation = changes.Value.BaseVelocity.HasValue ? remoteState.BaseVelocity - localState.BaseVelocity : null;
          Quaternion? rotationDeviation = changes.Value.Rotation.HasValue ? GetRotationDeviation(remoteState.Rotation, localState.Rotation) : null;

          // Apply deviations to the current state. we only want to override with the deviation adjustments
          var originalState = _playerStateHistory[NetworkTick.ClientTick];
          _playerStateHistory[NetworkTick.ClientTick] = originalState.OverrideStateWith(new NetworkPlayerState() {
            Position = originalState.Position + positionDeviation,
            BaseVelocity = originalState.BaseVelocity + velocityDeviation,
            Rotation = originalState.Rotation.HasValue && rotationDeviation.HasValue
              ? (originalState.Rotation.Value * rotationDeviation.Value).normalized
              : null
          });

          // Log soft reconcile events if enabled.
          if (showSoftReconciliationLog) {
            var reconcileSources = new List<string>();
            if (changes.Value.Position.HasValue) reconcileSources.Add("Position");
            if (changes.Value.Rotation.HasValue) reconcileSources.Add("Rotation");
            if (changes.Value.BaseVelocity.HasValue) reconcileSources.Add("Velocity");
            Debug.Log("Soft adjusted tick " + NetworkTick.ClientTick + " because (" + string.Join(", ", reconcileSources) + ")");
          }
        }
      }
    }

    #endregion

    /* Network Senders */

    #region Network Senders

    /// <summary>
    /// Sends input and state updates to all remote clients.
    /// Inputs are synced incrementally every tick, with periodic full syncs for accuracy and packet loss compensation.
    /// State updates occur at set intervals or when compensation requires a full sync.
    /// </summary>
    [Server]
    private void SendDataToClients() {
      var serverTick = NetworkTick.ServerAbsoluteTick;
      var isStateFullSyncTick = serverTick % stateSyncModulus == 0 || serverTick == _forceStateSyncTick;

      AddPlayerInputsToSendQueue();
      if (sendStateChanges || isStateFullSyncTick) AddPlayerStatesToSendQueue(isStateFullSyncTick);

      foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values) {
        // Skip the local server connection. And connections that are not yet active
        if (conn == NetworkServer.localConnection) continue;

        // Get this clients packet loss compensation
        // Determine if the client is still synchronizing.
        var isClientSynchronizing = !_syncedClients.Contains(conn.connectionId);
        var clientCompensation = NetworkTick.Server.GetClientToServerCompensation(conn.connectionId);

        // Ensure this client is not synchronizing, if client is synchronizing ensure full state
        SmartSendStatesToClient(conn, isClientSynchronizing, clientCompensation);
        if (_isPlayerReady) SmartSendInputsToClient(conn, isClientSynchronizing, clientCompensation);
      }
    }

    /// <summary>
    /// Sends client inputs to the server with packet loss compensation.
    /// Uses a full sync sequence if synchronizing; otherwise, sends incremental input changes.
    /// </summary>
    [Client]
    private void SmartSendInputsToServer(bool isSynchronizing, int compensation = 0) {
      // Calculate additional past ticks for packet loss compensation.
      var additionalPastTicks = durableInputs || compensation > 0 ? Math.Max(compensation, minCompensationTicks) : 0;
      var inputsToSend = isSynchronizing ? GetPlayerInputsSyncSequence(additionalPastTicks) : GetPlayerInputsSendSequence(additionalPastTicks);

      SendInputsToServer(inputsToSend);
    }

    /// <summary>
    /// Sends client input updates from the server to a specific client with packet loss compensation.
    /// Uses a full sync sequence during synchronization; otherwise, sends incremental input updates.
    /// </summary>
    [Server]
    private void SmartSendInputsToClient(NetworkConnectionToClient conn, bool isSynchronizing, int compensation = 0) {
      // get inputs to send with the correct compensation for packet loss
      var additionalPastTicks = durableInputs || compensation > 0 ? Math.Max(compensation, minCompensationTicks) : 0;
      var inputsToSend = isSynchronizing ? GetPlayerInputsSyncSequence(additionalPastTicks) : GetPlayerInputsSendSequence(additionalPastTicks);

      SendInputsToClient(conn, inputsToSend);
    }

    /// <summary>
    /// Sends state updates from the server to a specific client with packet loss compensation.
    /// Uses a full sync sequence when synchronizing; otherwise, sends incremental state changes.
    /// </summary>
    [Server]
    private void SmartSendStatesToClient(NetworkConnectionToClient conn, bool isSynchronizing, int compensation = 0) {
      // get inputs to send with the correct compensation for packet loss
      var additionalPastTicks = durableStates || compensation > 0 ? Math.Max(compensation, minCompensationTicks) : 0;
      var statesToSend = isSynchronizing ? GetPlayerStateSyncSequence(additionalPastTicks) : GetPlayerStatesSendSequence(additionalPastTicks);

      SendStatesToClient(conn, statesToSend);
    }

    /// <summary>
    /// Queues player inputs for sending. Uses a full input set on sync ticks (when absolute tick % inputsSyncModulus == 0) 
    /// or only the delta compared to the previous tick.
    /// </summary>
    private void AddPlayerInputsToSendQueue() {
      var currentTick = NetworkTick.CurrentTick;
      var isSendAbsoluteInputs = NetworkTick.CurrentAbsoluteTick % inputsSyncModulus == 0;

      // If the inputs are not set, return.
      if (!_playerInputsHistory[currentTick].HasTick) return;

      // Use absolute inputs or send only changes compared to the previous tick.
      var queuedInputs = isSendAbsoluteInputs
        ? _playerInputsHistory[currentTick]
        : _playerInputsHistory[currentTick]
          .GetChangedInputsComparedTo(_playerInputsHistory[NetworkTick.IncrementTick(currentTick, -1)], AdditionalInputsChangeCompare);

      // If changes exist, add them to the send queue.
      if (queuedInputs.HasValue) _inputsSendQueue[currentTick] = queuedInputs.Value;
    }

    /// <summary>
    /// Queues player state for sending. Sends the full state when isSendAbsoluteState is true; otherwise, 
    /// sends only the changes (delta) compared to the previous tick.
    /// </summary>
    private void AddPlayerStatesToSendQueue(bool isSendAbsoluteState) {
      var currentTick = NetworkTick.CurrentTick;

      // if the state is not set we can return
      if (!_playerStateHistory[currentTick].HasTick) return;

      var queuedState = isSendAbsoluteState
        ? _playerStateHistory[currentTick]
        : _playerStateHistory[currentTick]
          .GetChangedStateComparedTo(_playerStateHistory[NetworkTick.IncrementTick(currentTick, -1)], false, AdditionalStateChangeCompare);

      if (queuedState.HasValue) _stateSendQueue[currentTick] = queuedState.Value;
    }

    /// <summary> Retrieves queued player inputs over the compensation window starting from (CurrentTick - additionalPastInputsCount). </summary>
    private List<NetworkPlayerInputs> GetPlayerInputsSendSequence(int additionalPastInputsCount) {
      var inputsSequence = new List<NetworkPlayerInputs>();
      var offsetTick = NetworkTick.IncrementTick(NetworkTick.CurrentTick, -additionalPastInputsCount);

      // Add queued inputs for each tick within the compensation window.
      for (var i = 0; i <= additionalPastInputsCount; i++) {
        if (_inputsSendQueue[offsetTick].HasTick) inputsSequence.Add(_inputsSendQueue[offsetTick]);
        offsetTick = NetworkTick.IncrementTick(offsetTick, 1);
      }

      return inputsSequence;
    }

    /// <summary> Retrieves queued player states over the compensation window starting from (CurrentTick - additionalPastInputsCount). </summary>
    private List<NetworkPlayerState> GetPlayerStatesSendSequence(int additionalPastInputsCount) {
      var statesSequence = new List<NetworkPlayerState>();
      var offsetTick = NetworkTick.IncrementTick(NetworkTick.CurrentTick, -additionalPastInputsCount);

      // Add queued states for each tick within the compensation window.
      for (var i = 0; i <= additionalPastInputsCount; i++) {
        if (_stateSendQueue[offsetTick].HasTick) statesSequence.Add(_stateSendQueue[offsetTick]);
        offsetTick = NetworkTick.IncrementTick(offsetTick, 1);
      }

      return statesSequence;
    }

    /// <summary> Retrieves a sync sequence of player inputs starting with an absolute input, then only changed inputs for subsequent ticks. </summary>
    private List<NetworkPlayerInputs> GetPlayerInputsSyncSequence(int additionalPastInputsCount) {
      var inputsSequence = new List<NetworkPlayerInputs>();
      var offsetTick = NetworkTick.IncrementTick(NetworkTick.CurrentTick, -additionalPastInputsCount);

      // Ensure the first tick is absolute.
      if (_playerInputsHistory[offsetTick].HasTick) inputsSequence.Add(_playerInputsHistory[offsetTick]);

      // Send only changes after the first full sync.
      var previousTick = offsetTick;
      for (var i = 0; i < additionalPastInputsCount; i++) {
        offsetTick = NetworkTick.IncrementTick(offsetTick, 1);
        if (_playerInputsHistory[offsetTick].HasTick) {
          var changes = _playerInputsHistory[offsetTick].GetChangedInputsComparedTo(_playerInputsHistory[previousTick], AdditionalInputsChangeCompare);
          if (changes.HasValue) inputsSequence.Add(changes.Value);
        }

        previousTick = offsetTick;
      }

      return inputsSequence;
    }

    /// <summary> Retrieves a sync sequence of player states starting with an absolute state, then only changes compared to the previous tick. </summary>
    private List<NetworkPlayerState> GetPlayerStateSyncSequence(int additionalPastInputsCount) {
      var stateSequence = new List<NetworkPlayerState>();
      var offsetTick = NetworkTick.IncrementTick(NetworkTick.CurrentTick, -additionalPastInputsCount);

      // Ensure the first tick is absolute.
      if (_playerStateHistory[offsetTick].HasTick) stateSequence.Add(_playerStateHistory[offsetTick]);

      // We want to send only changes after the first full sync, the peer can reconstruct them by overlaying one on top of another during execution
      var previousTick = offsetTick;
      for (var i = 0; i < additionalPastInputsCount; i++) {
        offsetTick = NetworkTick.IncrementTick(offsetTick, 1);
        if (_playerStateHistory[offsetTick].HasTick) {
          var changes = _playerStateHistory[offsetTick].GetChangedStateComparedTo(_playerStateHistory[previousTick], false, AdditionalStateChangeCompare);
          if (changes.HasValue) stateSequence.Add(changes.Value);
        }

        previousTick = offsetTick;
      }

      return stateSequence;
    }

    #endregion

    /* Network Data Receivers */

    #region Network Data Recievers

    /// <summary> Marks the client as synchronized by adding its connection ID to the synced clients set. </summary>
    [Command(channel = Channels.Reliable, requiresAuthority = false)]
    private void ClientSynchronizedCmd(NetworkConnectionToClient connectionToClient = null) {
      if (connectionToClient is not null) _syncedClients.Add(connectionToClient.connectionId);
    }

    /// <summary> Processes input updates from a client by updating the received inputs history and setting the remote player's start tick if needed. </summary>
    [Server]
    private void OnInputsFromClient(List<NetworkPlayerInputs> inputsList, int connectionId) {
      foreach (var inputs in inputsList)
        if (inputs.TickNumber.HasValue)
          _receivedPlayerInputs[inputs.TickNumber.Value] = _receivedPlayerInputs[inputs.TickNumber.Value]
            .OverrideInputsWith(inputs, inputs.TickNumber.Value, AdditionalInputsOverride);

      if (!_isPlayerReady) {
        _syncedClients.Add(connectionId);
        SetRemotePlayerStart(inputsList);
      }
    }

    /// <summary> Processes input updates from the server by updating the received inputs history and setting the remote player's start tick if not yet set. </summary>
    [Client]
    private void OnInputsFromServer(List<NetworkPlayerInputs> inputsList) {
      foreach (var inputs in inputsList)
        if (inputs.TickNumber.HasValue)
          _receivedPlayerInputs[inputs.TickNumber.Value] = _receivedPlayerInputs[inputs.TickNumber.Value]
            .OverrideInputsWith(inputs, inputs.TickNumber.Value, AdditionalInputsOverride);

      if (!_isPlayerReady) SetRemotePlayerStart(inputsList);
    }

    /// <summary> Processes state updates from the server by updating the received states history. </summary>
    [Client]
    private void OnStateFromServer(List<NetworkPlayerState> statesList) {
      foreach (var state in statesList)
        if (state.TickNumber.HasValue)
          _receivedPlayerStates[state.TickNumber.Value] =
            _receivedPlayerStates[state.TickNumber.Value].OverrideStateWith(state, state.TickNumber.Value, AdditionalStateOverride);
    }

    #endregion

    /* Network Senders Abstractions */

    #region Network Senders Abstractions

    /// <summary> Sends one or multiple input updates from client to server, optimizing bandwidth. </summary>
    [Client]
    private void SendInputsToServer(List<NetworkPlayerInputs> inputs) {
      // Optimize bandwidth by sending a single update if only one input differs, or a sequence if multiple do.
      if (inputs.Count == 1) CmdSendInputsToServer(inputs[0]);
      else if (inputs.Count > 1) CmdSendInputsSequenceToServer(inputs);
      // default send none
    }

    /// <summary> Sends one or multiple input updates from server to a specific client, optimizing bandwidth. </summary>
    [Server]
    private void SendInputsToClient(NetworkConnectionToClient conn, List<NetworkPlayerInputs> inputs) {
      // Optimize bandwidth by sending a single update if only one state differs, or a sequence if multiple do.
      if (inputs.Count == 1) RpcSendInputsToClient(conn, inputs[0]);
      if (inputs.Count > 1) RpcSendInputsSequenceToClient(conn, inputs);
      // default send none
    }

    /// <summary> Sends one or multiple state updates from server to a specific client, optimizing bandwidth. </summary>
    [Server]
    private void SendStatesToClient(NetworkConnectionToClient conn, List<NetworkPlayerState> states) {
      // Optimize bandwidth by sending a single update if only one state differs, or a sequence if multiple do.
      if (states.Count == 1) RpcSendStatesToClient(conn, states[0]);
      else if (states.Count > 1) RpcSendStatesSequenceToClient(conn, states);
      // default send none
    }

    #endregion

    /* Network Receivers Abstractions */

    #region Network Receivers Abstractions

    /// <summary>Sends a single input from the client to the server.</summary>
    [Command(channel = Channels.Unreliable, requiresAuthority = true)]
    private void CmdSendInputsToServer(NetworkPlayerInputs inputs, NetworkConnectionToClient connectionToClient = null) =>
      OnInputsFromClient(new List<NetworkPlayerInputs> { inputs }, connectionToClient?.connectionId ?? 0);

    /// <summary>Sends a sequence of inputs from the client to the server.</summary>
    [Command(channel = Channels.Unreliable, requiresAuthority = true)]
    private void CmdSendInputsSequenceToServer(List<NetworkPlayerInputs> inputsSequence, NetworkConnectionToClient connectionToClient = null) =>
      OnInputsFromClient(inputsSequence, connectionToClient?.connectionId ?? 0);

    /// <summary>Sends a single input from the server to a specific client.</summary>
    [TargetRpc(channel = Channels.Unreliable)]
    private void RpcSendInputsToClient(NetworkConnection target, NetworkPlayerInputs inputs) => OnInputsFromServer(new List<NetworkPlayerInputs> { inputs });

    /// <summary>Sends a sequence of inputs from the server to a specific client.</summary>
    [TargetRpc(channel = Channels.Unreliable)]
    private void RpcSendInputsSequenceToClient(NetworkConnection target, List<NetworkPlayerInputs> inputsSequence) => OnInputsFromServer(inputsSequence);

    /// <summary>Sends a single state from the server to a specific client.</summary>
    [TargetRpc(channel = Channels.Unreliable)]
    private void RpcSendStatesToClient(NetworkConnection target, NetworkPlayerState state) => OnStateFromServer(new List<NetworkPlayerState> { state });

    /// <summary>Sends a sequence of states from the server to a specific client.</summary>
    [TargetRpc(channel = Channels.Unreliable)]
    private void RpcSendStatesSequenceToClient(NetworkConnection target, List<NetworkPlayerState> stateSequence) => OnStateFromServer(stateSequence);

    #endregion

    /* Helper Functions */

    #region Helper Functions

    /// <summary> Ends simulation when the current server tick exceeds the simulation end tick. </summary>
    private void CheckSimulationEnd() {
      if (_isSimulating && NetworkTick.SubtractTicks(NetworkTick.ServerTick, _simulationEndTick) > 0) _isSimulating = false;
    }

    /// <summary> Retrieves and updates player inputs for the specified tick. </summary>
    private NetworkPlayerInputs GetPlayerInputsWithTick(int tick) {
      var inputs = GetPlayerInputs();
      inputs.TickNumber = tick;
      return inputs;
    }

    /// <summary> Retrieves the player state for the specified tick by comparing with the previous state to avoid jitter. </summary>
    private NetworkPlayerState GetPlayerStateWithTick(int currentTick) {
      var newState = GetPlayerState();

      var previousTick = NetworkTick.IncrementTick(currentTick, -1);
      var changes = newState.GetChangedStateComparedTo(_playerStateHistory[previousTick], false, AdditionalStateChangeCompare) ?? new NetworkPlayerState();
      var stableState = _playerStateHistory[previousTick].OverrideStateWith(changes, currentTick, AdditionalStateOverride);

      return stableState;
    }

    /// <summary> Clears old historical data for predicted and received player inputs and states, and resets the pending reset flag. </summary>
    private void ClearPastTickData() {
      var clearTick = NetworkTick.IncrementTick(NetworkTick.CurrentTick, -1000);
      // Reset predicted inputs and state data for the old tick.
      _playerInputsHistory[clearTick] = new NetworkPlayerInputs();
      _playerStateHistory[clearTick] = new NetworkPlayerState();

      // Reset received inputs and state data for the old tick.
      _receivedPlayerInputs[clearTick] = new NetworkPlayerInputs();
      _receivedPlayerStates[clearTick] = new NetworkPlayerState();

      // Reset send queues for the old tick.
      _inputsSendQueue[clearTick] = new NetworkPlayerInputs();
      _stateSendQueue[clearTick] = new NetworkPlayerState();

      // Clear pending reset flag.
      _isPendingStateReset = false;
    }

    /// <summary> Calculates the deviation between two rotations and returns the shortest rotation path. </summary>
    private Quaternion? GetRotationDeviation(Quaternion? q1, Quaternion? q2) {
      if (!q1.HasValue || !q2.HasValue) return null;

      Quaternion deviation = Quaternion.Inverse(q2.Value) * q1.Value;
      // Negate the quaternion to get the equivalent rotation via shorter path otherwise return the deviation
      return deviation.w < 0 ? new Quaternion(-deviation.x, -deviation.y, -deviation.z, -deviation.w) : deviation;
    }

    #endregion
  }
}