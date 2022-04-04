using System;
using Mirror;
using UnityEngine;

namespace Mirror
{

 /*
 * We want to manage ping frequency based on the situation rather than static:
 * Idle - When all is well and the tick is in sync with the incoming information
 * Initial - When we first connect we want to quickly scan the network state
 * Verifying - When a change was detected we want to allow for more pings to verify that the change is real and not a spike
 * Accelerated - When there is a change in ping we want to give more pings
 */

#region Network Scripts Structs

  public enum TickPingState {
    Idle,
    Initial,
    Verifying,
    Accelerated,
  }

  /* Structs for Ping/Pong with the host/server */
  public struct ServerPingPayload {
    public int ClientTickNumber;
  }

  public struct ServerPongPayload {
    public int ClientTickNumber;
    public int ServerTickNumber;
  }

  /* Struct for saved ping item - used on the client to compare */
  public struct PingItem {
    public int ClientTickOffset; // Offset ( 1/2 rtt converted to ticks )
    public int TickDiff;         // Difference between Client and Server Ticks
  }

#endregion

  public class NetworkTickSyncer : NetworkBehaviour {
  #region Controller Configurations

    /* Controller Configurations */
    private static int TickHistorySize   = 128; // Circular buffer size
    private static int InitialTicksCount = 12;  // How many ticks required for init to be completed ( state becomes ready after x ticks )

    private static int PingTickAverageSize               = 12; // How many ticks required for averaging out rtt and server tick
    private static int PingConsolidationAllowedDeviation = 1;  // Tick offset ( 1/2 rtt ) forgiveness - we dont want to adjust ticks when there is a small deviation
    private static int PingAdjustmentSize                = 30; // How many times does the tick has to be smaller than PingConsolidationAllowedDeviation to adjust regardless
    private static int AcceleratedPingConsolidationSize  = 12; // How many ticks should the accelerated state run for before verifying
    private static int TickDiffConsolidationSize         = 12; // How many ticks to consider when Server Network Tick is calculated
    private static int TickConsolidationVerificationSize = 30; // How many times the ping has to be different for the system to adjust Network Tick ( in case network improved by 1 tick )

    /* Used to time ping intervals during Network Controller States */
    private int InitialPingIntervalMs     = 100;
    private int VerifyingPingIntervalMs   = 100;
    private int AcceleratedPingIntervalMs = 200;
    private int IdlePingIntervalMs        = 500;

  #endregion

  #region Static Instanciation

    /* Instantiate instances - used for Static level access */
    private static NetworkTickSyncer _instance;
    private static NetworkTick       _networkTick;
    private static bool              _isReady = false;

    public static NetworkTickSyncer Instance => _instance;
    public static NetworkTick       Tick     => _networkTick;
    public static bool              IsReady  => _isReady;

    /* Instantiate NetworkTick static Instance locally + Add Network Controller as well */
    private void Awake() {
      if (_instance != null && _instance != this) {
        Destroy(this.gameObject);
      }
      else {
        _instance = this;
        _networkTick = new NetworkTick();
      }
    }

  #endregion

  #region Private Variables

    /* Public Variables */
    public bool IsDebug = false;

    /* Private Variables */
    private TickPingState _tickPingState       = TickPingState.Initial;
    private PingItem[]    _tickHistory         = new PingItem[TickHistorySize]; // Circular buffer ping item history
    private int           _tickPingCount       = 0;                             // Number of pings ( total )
    private int           _skipPhysicsSteps    = 0;                             // How many physics steps to skip
    private int           _forwardPhysicsSteps = 0;                             // How many physics steps to fast forward
    private int           _clientTickNumber    = 0;                             // Number of ticks on the client - used for measuring tick offsets


    private ServerPongPayload _lastReceivedPong;                // We only save 1 last ping ( in case we receive multiple pings per fixed Update )
    private bool              _isReceivedPong          = false; // Flag to tell the code to do ping verification
    private int               _previousTickPingOffset  = 0;     // Used to detect when ping has done adjusting
    private int               _pingLastDiff            = 0;     // Last difference in ping
    private int               _pingConsistentDiffCount = 0;     // Used to count how consistent is the difference in ping ( can trigger lazy ping update )
    private int               _acceleratedPingCount    = 0;     // Count pings when Network Controller is in Accelerated state
    private int               _verifyingTickCount      = 0;     // Count pings when Network Controller is in Verifying state
    private float             _lastPingTime            = 0;     // Used to measure time between pings - variable depending on the state
    private int               _deSyncIdleCounter       = 0;     // Counts de-syncs in idle mode - Dont panic after 1 de-sync - wait for
    private int               _pingInSyncCounter       = 0;     // Wait for 3 consistent pings to apply any changes ( group stutters )

  #endregion

  #region Client Functions

    /*************************/
    /* Client Only functions */
    /*************************/
    [Client] // Queue physics adjustment
    private void AdjustClientPhysicsTick(int tickAdjustment) {
      if (tickAdjustment > 0) {
        _forwardPhysicsSteps += tickAdjustment;
      }
      else {
        _skipPhysicsSteps += -tickAdjustment;
      }
    }

    [Client] // Update Client tick number based on Time.time
    private void UpdateClientTick() {
      _clientTickNumber = Mathf.RoundToInt((float) (Time.time * _networkTick.GetServerTickPerSecond())); // Convert time to Ticks
    }

    [Client] // Update server tick based on offset
    private void UpdateServerTick() {
      _networkTick.SetServerTick(_clientTickNumber - _networkTick.GetTickLocalOffset());
    }

    [Client] // We are not always comparing ticks so we want to move server tick locally even if no pings are happening
    private void AdvanceServerTick() {
      _networkTick.SetServerTick(_networkTick.GetServerTick() + 1);
    }

  #endregion

  #region Client Server Communication

    /*******************************/
    /* Client Server Communication */
    /*******************************/
    [Command(requiresAuthority = false, channel = Channels.Unreliable)]
    private void CmdPingServer(ServerPingPayload clientPing, NetworkConnectionToClient sender = null) {
      // Once we got ping from client we want to send the current server tick immediately

      RpcServerPong(sender, new ServerPongPayload() {
        ClientTickNumber = clientPing.ClientTickNumber,
        ServerTickNumber = _networkTick.GetServerTick(),
      });
    }

    [TargetRpc(channel = Channels.Unreliable)]
    private void RpcServerPong(NetworkConnection _, ServerPongPayload serverPong) {
      /* We only want to get the most recent pong from the server and ignore duplicates or throttled pongs */
      if (_lastReceivedPong.ServerTickNumber < serverPong.ServerTickNumber || _lastReceivedPong.ClientTickNumber < serverPong.ClientTickNumber) {
        _lastReceivedPong = serverPong;
        _isReceivedPong = true;
      }
    }

    [Client]
    private void TickPing() {
      /* We want to decide when to sent the ping - i use this funky method because its convenient */
      int milliseconds = Mathf.RoundToInt((float) (Time.time * 1000));

      bool queuePing = false;
      switch (_tickPingState) {
        case TickPingState.Initial:
          queuePing = milliseconds - _lastPingTime > InitialPingIntervalMs;
          break;
        case TickPingState.Verifying:
          queuePing = milliseconds - _lastPingTime > VerifyingPingIntervalMs;
          break;
        case TickPingState.Accelerated:
          queuePing = milliseconds - _lastPingTime > AcceleratedPingIntervalMs;
          break;
        case TickPingState.Idle:
          queuePing = milliseconds - _lastPingTime > IdlePingIntervalMs;
          break;
      }

      if (queuePing) {
        _lastPingTime = milliseconds;
        CmdPingServer(new ServerPingPayload() {
          ClientTickNumber = _clientTickNumber,
        });
      }
    }

  #endregion

  #region Server Pong Handling

    /******************************************/
    /* Server Pong Handling and Consolidation */
    /******************************************/


    /*
     * Adjust RTT ticks (can trigger physics tick adjustment)
     * We want to consider adjusting ping if it changes: isSilent wont trigger Network State Change
     */

    [Client]
    private void ConsiderPingAdjustment(bool isIdle) {
      int newOffset = GetTickPingAverage();
      int pingDiff = newOffset - _networkTick.GetClientTickOffset();
      if (pingDiff == 0) {
        DecreasePingConsistency();
        _previousTickPingOffset = newOffset;
        return;
      }

      // If is not silent we want to see if ping has consistent fluctuations and adjust accordingly
      // Its used when ping is 1 forward or backwards for >50% of the time ( we are on the edge )
      if (isIdle) { //PS: used in idle mode
        if (_pingLastDiff == pingDiff && pingDiff != 0) {
          IncreasePingConsistency();
        }
        else {
          DecreasePingConsistency();
        }
      }


      _pingLastDiff = pingDiff;
      if (Math.Abs(pingDiff) > PingConsolidationAllowedDeviation || GetPingConsistency() > PingAdjustmentSize) {
        // Event if we are ready to adjust we will wait until ping is consistent ( not going up or down ) to avoid multiple stutters
        if (newOffset == _previousTickPingOffset) {
          _pingInSyncCounter++;
        }
        else {
          _pingInSyncCounter = 0;
        }

        // Wait for 3 consistent pings before applying ( maybe ping is in flux )
        if (_pingInSyncCounter > 2) {
          _networkTick.SetClientTickOffset(newOffset);
          AdjustClientPhysicsTick(pingDiff);
          ResetPingConsistency();
          _pingInSyncCounter = 0;
          string adjustmentDirection = pingDiff > 0 ? "+" : "";
          Debug.Log($"@Client Prediction Tick Adjusted [{adjustmentDirection}{pingDiff}] -> new Offset [{newOffset}]");
        }

        // if is not silent we want to trigger accelerated Network Controller state to verify ping faster
        if (isIdle) {
          _tickPingState = TickPingState.Accelerated;
        }
      }

      _previousTickPingOffset = newOffset;
    }


    /*
     * Adjust Server Network Tick sync (can trigger physics tick adjustment)
     * We consider physics engine adjustment here
     * We want to change only in case the difference is nearing 1 tick and not before to avoid spikes
     * We also count and deduct verify attempts to save traffic - most cases the verification only lasts for 2-3 pings and the state is returned to idle
     */
    [Client]
    private void ConsolidateServerTickNumber() {
      int localNetworkTick = _networkTick.GetServerTick();
      float average = GetTickDiffAverage();

      if (Mathf.Abs(average) > 0.9) {
        if (_verifyingTickCount > TickConsolidationVerificationSize) {
          int tickAdjustment = average > 0 ? Mathf.CeilToInt(average) : Mathf.FloorToInt(average);
          AdjustClientPhysicsTick(tickAdjustment);
          _networkTick.SetServerTick(localNetworkTick + tickAdjustment);
          _networkTick.SetTickLocalOffset(_clientTickNumber - (localNetworkTick + tickAdjustment));
          _verifyingTickCount = 0;
          _tickPingState = TickPingState.Idle;
          string adjustmentDirection = tickAdjustment > 0 ? "+" : "";
          Debug.Log($"@Client Network Tick Adjusted [{adjustmentDirection}{tickAdjustment}] -> new Network Tick [{localNetworkTick + tickAdjustment}]");
        }
        else {
          _verifyingTickCount++;
        }
      }
      else {
        if (_verifyingTickCount > 0) {
          _verifyingTickCount--;
        }

        if (_verifyingTickCount == 0 && _tickPingState == TickPingState.Verifying) {
          _tickPingState = TickPingState.Idle;
        }
      }
    }

    [Client]
    private void HandleServerPong() {
      if (!_isReceivedPong) return; // If no pongs were received - exit function
      _isReceivedPong = false;
      ServerPongPayload serverPong = _lastReceivedPong; // if Pong was received set as pong to handle

      /* Calculate basic info about received pong */
      int localNetworkTick = _networkTick.GetServerTick();                     // current server tick on the client
      int clientTickPing = _clientTickNumber - serverPong.ClientTickNumber;    // How much ticks passed since the ping was sent
      int estimatedClientOffset = Mathf.CeilToInt((float) clientTickPing / 2); // Estimated offset in ticks - 1.5 we count as 2 etc

      /* Calculate server tick candidate that will be used in case of tick adjustment */
      int serverTickCandidate = serverPong.ServerTickNumber + estimatedClientOffset;
      /* Save estimation in tick history */
      AddPing(new PingItem() {
        TickDiff = serverTickCandidate - localNetworkTick,
        ClientTickOffset = estimatedClientOffset,
      });

      /* Decide what to do based on current Network Controller State */
      switch (_tickPingState) {
        case TickPingState.Initial:
          // Initially we just run through the first X pings to get enough information to initialzie everything
          // We just set ticks - works fairly well - TODO: Implement averaging out logic
          _networkTick.SetServerTick(serverTickCandidate);
          _networkTick.SetTickLocalOffset(_clientTickNumber - serverTickCandidate);

          if (!_isReady && _tickPingCount > InitialTicksCount) {
            _networkTick.SetClientTickOffset(GetTickPingAverage());       // Average out RTT and set
            _previousTickPingOffset = _networkTick.GetClientTickOffset(); // Save last average state
            _tickPingState = TickPingState.Idle;                          // Set Network Controller state to idle
            _isReady = true;                                              // Set isReady to true
            _networkTick.SetIsReady(true);
            Debug.Log($"@Client Ready NetworkTick:[{_networkTick.GetServerTick()}] Network Offset:[{_networkTick.GetClientTickOffset()}]");
          }

          break;
        case TickPingState.Accelerated:
          // Used after server tick of ping tick are changed - we want to get clean ticks to check
          _acceleratedPingCount++;
          if (_acceleratedPingCount > AcceleratedPingConsolidationSize) {
            _acceleratedPingCount = 0;
            _tickPingState = TickPingState.Idle;
          }

          ConsiderPingAdjustment(false); // Cosnider updating ping but dont trigger Network State change
          break;
        case TickPingState.Verifying:
          // If we dettected Server Network Tick change we want to verify that it was not a single occurrence or a spike
          ConsolidateServerTickNumber();
          ConsiderPingAdjustment(false);
          break;

        case TickPingState.Idle:
          // When idling - always check if there are fluctuations in ping or network time ( server got stuck or client got stuck )
          ConsiderPingAdjustment(true);
          if (localNetworkTick != serverTickCandidate) {
            _deSyncIdleCounter++;
            if (_deSyncIdleCounter > 2) {
              _tickPingState = TickPingState.Verifying;
              _deSyncIdleCounter = 0;
            }
          }
          else {
            _deSyncIdleCounter = 0;
          }

          break;
      }

      _isReceivedPong = false;
    }

  #endregion

  #region Fixed Update Handling

    /*************************/
    /* Fixed Update Handling */
    /************************/
    [Client]
    private void ClientFixedUpdate() {
      TickPing();          //Check if we want to send ping to server
      UpdateClientTick();  // Update local tick
      UpdateServerTick();  // Update server tick ( based on local tick and local tick offset )
      HandleServerPong();  // Handle server pongs if any arrived + consolidate server ticks
      AdvanceServerTick(); // Advance server tick by 1 - even if we are synced with the server we need to step forward to be 0 or 1 ticks ahead of the server
      PhysicsStepHandle(); // Move physics or skip or fast forward
      if (IsDebug) {
        DebugLogTimeDiff();
      }
    }

    [Server]
    private void ServerFixedUpdate() {
      int nextServerTick = Mathf.RoundToInt((float) (NetworkTime.time * _networkTick.GetServerTickPerSecond()));
      _networkTick.SetServerTick(nextServerTick);
      _isReady = true;
      PhysicStep(Time.deltaTime);
    }

    private void FixedUpdate() {
      if (isServer) {
        ServerFixedUpdate();
      }
      else {
        ClientFixedUpdate();
      }
    }

    private void DebugLogTimeDiff() {
      if (_isReady && _clientTickNumber % 50 == 0) {
        int networkTimeTick = Mathf.RoundToInt((float) (NetworkTime.time * _networkTick.GetServerTickPerSecond()));
        string state = "Idle";
        switch (_tickPingState) {
          case TickPingState.Verifying:
            state = "Verifying";
            break;
          case TickPingState.Initial:
            state = "Initial";
            break;
          case TickPingState.Accelerated:
            state = "Accelerated";
            break;
        }

        Debug.Log(
          $"ClientNetworkTick:[{_networkTick.GetServerTick()}] ClientPredictionTick:[{_networkTick.GetPredictionTick()}] ClientTickOffset:[{_networkTick.GetClientTickOffset()}] NetworkTimeTick:[{networkTimeTick}] verifyingTickCount:[{_verifyingTickCount}]:{state}");
      }
    }

  #endregion

  #region Physics Handling

    /***************************/
    /* Client Physics Handling */
    /***************************/
    public virtual void PhysicStep(float deltaTime) { }


    [Client]
    private void PhysicsStepHandle() {
      float fixedDeltaTime = Time.fixedDeltaTime;
      float deltaTime = Time.deltaTime;
      if (_skipPhysicsSteps > 0) {
        _skipPhysicsSteps = PhysicStepSkip(deltaTime, _skipPhysicsSteps);
        // Fix user generated errors
        if (_skipPhysicsSteps < 0) {
          _skipPhysicsSteps = 0;
        }

        return;
      }

      if (_forwardPhysicsSteps > 0) {
        // include current tick simulation + fast forwarded one ( for simple overrides )
        _forwardPhysicsSteps = PhysicStepFastForward(deltaTime, fixedDeltaTime, _forwardPhysicsSteps);
        // Fix user generated errors
        if (_forwardPhysicsSteps < 0) {
          _forwardPhysicsSteps = 0;
        }

        return;
      }

      PhysicStep(deltaTime);
    }


    [Client]
    public virtual int PhysicStepSkip(float deltaTime, int skipSteps) {
      Debug.Log($"Ignored 'PhysicStep' step and calling PhysicStepSkip( {skipSteps}, {deltaTime} )");
      return skipSteps - 1; // In case someone wants to skip more than 1 step on each FixedUpdate
    }

    [Client]
    public virtual int PhysicStepFastForward(float deltaTime, float fixedDeltaTime, int fastForwardSteps) {
      Debug.Log($"Ignored 'PhysicStep' step and calling PhysicStepFastForward( {fastForwardSteps}, {deltaTime}/{fixedDeltaTime} )");
      return 0; //In case someone wants to fast forward ticks not all at once
    }

  #endregion

  #region Helper Functions

    /********************************************************/
    /* Helper functions used to calculate and adjust things */
    /********************************************************/

    /* Idle ping consistency counter functions */
    private int GetPingConsistency() {
      return _pingConsistentDiffCount;
    }

    private void IncreasePingConsistency() {
      _pingConsistentDiffCount++;
    }

    private void DecreasePingConsistency() {
      if (_pingConsistentDiffCount > 0) {
        _pingConsistentDiffCount--;
      }
    }

    private void ResetPingConsistency() {
      _pingConsistentDiffCount = 0;
    }

    /* Ping history management */
    private PingItem GetPing(int index) {
      return _tickHistory[index % TickHistorySize];
    }

    private int AddPing(PingItem pingItem) {
      _tickHistory[_tickPingCount % TickHistorySize] = pingItem;
      _tickPingCount++;
      return _tickPingCount;
    }

    /* Find the position of the highest and lowest Client Tick Offset ( deviation between running server tick and remote tick candidate ) */
    private (int, int) GetTickPingAverageMaxIndex() {
      PingItem initialItem = GetPing(_tickPingCount);
      int minIndex = _tickPingCount;
      int maxIndex = _tickPingCount;
      int max = initialItem.TickDiff;
      int min = initialItem.TickDiff;
      for (int i = 0; i <= PingTickAverageSize; i++) {
        int pingIndex = _tickPingCount - i;
        PingItem item = GetPing(pingIndex);
        if (max < item.ClientTickOffset) {
          max = item.ClientTickOffset;
          maxIndex = pingIndex;
        }

        if (min > item.ClientTickOffset) {
          min = item.ClientTickOffset;
          minIndex = pingIndex;
        }
      }

      return (maxIndex, minIndex);
    }

    /* Average out ping numbers - exclude highest and lowest ping numbers ( ignores short spikes ) */
    private int GetTickPingAverage() { //TODO: Cosnider using smoothing algorithms
      (int maxIndex, int minIndex) = GetTickPingAverageMaxIndex();
      int sumCounter = 0;
      float sum = 0;
      for (int i = 0; i <= PingTickAverageSize; i++) {
        int pingIndex = _tickPingCount - i;
        PingItem item = GetPing(pingIndex);
        if (pingIndex != maxIndex && pingIndex != minIndex) {
          sum += item.ClientTickOffset;
          sumCounter++;
        }
      }

      return Mathf.FloorToInt(sum / sumCounter);
    }

    // Get position of max and min tick diff - try to avoid spikes
    private (int, int) GetTickDiffAverageMaxIndex() {
      PingItem initialItem = GetPing(_tickPingCount);
      int minIndex = _tickPingCount;
      int maxIndex = _tickPingCount;
      int max = initialItem.TickDiff;
      int min = initialItem.TickDiff;

      for (int i = 0; i <= TickDiffConsolidationSize; i++) {
        int pingIndex = _tickPingCount - i;
        PingItem item = GetPing(pingIndex);
        if (max < item.TickDiff) {
          max = item.TickDiff;
          maxIndex = pingIndex;
        }

        if (min > item.TickDiff) {
          min = item.TickDiff;
          minIndex = pingIndex;
        }
      }

      return (maxIndex, minIndex);
    }

    // Average out last X tick diffs and exclude highest diff to avoid spikes
    private float GetTickDiffAverage() {
      (int maxIndex, int minIndex) = GetTickDiffAverageMaxIndex();
      int sumCounter = 0;
      float sum = 0;
      for (int i = 0; i <= TickDiffConsolidationSize; i++) {
        int pingIndex = _tickPingCount - i;
        PingItem item = GetPing(pingIndex);
        if (pingIndex != maxIndex && pingIndex != minIndex) {
          sum += item.TickDiff;
          sumCounter++;
        }
      }

      return sum / sumCounter;
    }

  #endregion
  }
}
