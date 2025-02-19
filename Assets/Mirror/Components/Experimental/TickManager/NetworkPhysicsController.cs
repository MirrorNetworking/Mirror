using UnityEngine;
using System;

namespace Mirror.Components.Experimental{
  [DefaultExecutionOrder(-10)]
  [DisallowMultipleComponent]
  [AddComponentMenu("Network/Network Physics Controller")]
  public class NetworkPhysicsController : MonoBehaviour{
    // reconcile tick and request status
    private static bool _pendingReconcile = false;
    private static int _reconcileStartTick = 0;

    /// <summary>
    /// Callback action to handle tick-forwarding logic.
    /// Allows external classes to define custom behavior when the tick advances.
    /// </summary>
    public Action<int> TickForwardCallback;

    /// <summary>
    /// Subscribable callback action to handle reset state logic.
    /// Allows external classes to define custom behavior on reset state.
    /// </summary>
    public static event Action OnResetState;

    /// <summary>
    /// Subscribable callback action to handle synchronized logic.
    /// Allows external classes to define custom behavior on network synchronization.
    /// </summary>
    public static event Action OnSynchronized;

    /// <summary>
    /// Advances the game state by a specified number of ticks.
    /// Invokes the TickForwardCallback to allow external classes to handle tick-forwarding logic.
    /// Typically called with `deltaTicks` = 1 from RunSimulate.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks to forward.</param>
    public virtual void TickForward(int deltaTicks) {
      TickForwardCallback?.Invoke(deltaTicks);
    }

    /// <summary>
    /// Called when network is synchronized. Invokes the <see cref="OnSynchronized"/> if it is not null.
    /// </summary>
    public virtual void NetworkSynchronized() {
      OnSynchronized?.Invoke();
      NetworkPhysicsEntity.RunNetworkSynchronized();
    }

    /// <summary>
    /// Called just before the reconcile process is performed. Invokes the <see cref="OnResetState"/> if it is not null.
    /// </summary>
    public virtual void ResetNetworkState() {
      // First, call all callbacks to let non-networked items reset their states before networked items.
      // Otherwise, networked items might rely on out-of-sync world data (e.g., positions) if non-networked items aren’t reset first.
      OnResetState?.Invoke();
      NetworkPhysicsEntity.RunResetNetworkState();
    }

    /// <summary>
    /// Executes a single physics simulation step for the given delta time.
    /// Uses Unity's Physics.Simulate to perform the physics tick.
    /// Typically called with Time.fixedDeltaTime.
    /// </summary>
    /// <param name="deltaTime">The time interval to simulate physics for.</param>
    public virtual void PhysicsTick(float deltaTime) {
      Physics.Simulate(deltaTime); // Using Unity's built-in physics engine.
    }

    /// <summary>
    /// Runs the simulation for the specified number of delta ticks.
    /// This method performs multiple steps of entity updates and physics ticks
    /// to bring the simulation in sync with the latest tick count.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks to simulate forward.</param>
    /// <param name="isReconciling">Is current simulation a reconciliation.</param>
    public void RunSimulate(int deltaTicks, bool isReconciling = false) {
      // ensure that the are ticks to execute in the first place
      if (deltaTicks<1) return;

      // execute the first tick
      TickForward(1);

      // If reconciling, reset the network state before simulating.
      if (isReconciling) ResetNetworkState();
      SimulateTick();

      // run additional ticks iteration if any left skipping the first step since it was already executed
      for (var step = 1; step < deltaTicks; step++) {
        TickForward(1);
        SimulateTick();
      }

      // If reconciling, run post-reconciliation actions.
      if (isReconciling) NetworkPhysicsEntity.RunAfterReconcile();
    }

    /// <summary> Performs a single simulation step by invoking pre-updates, network updates, physics simulation, and post-updates for one tick. </summary>
    public void SimulateTick() {
      var deltaTime = Time.fixedDeltaTime;
      NetworkPhysicsEntity.RunBeforeNetworkUpdates(1, deltaTime);
      NetworkPhysicsEntity.RunNetworkUpdates(1, deltaTime);
      PhysicsTick(deltaTime);
      NetworkPhysicsEntity.RunAfterNetworkUpdates(1, deltaTime);
    }

    /// <summary> Requests the reconciliation process to start from a specific tick (including the requested tick ) </summary>
    /// <param name="reconcileStartTick">The tick from which to start reconciliation.</param>
    public static void RequestReconcileFromTick(int reconcileStartTick) {
      if (!_pendingReconcile || NetworkTick.SubtractTicks(_reconcileStartTick, reconcileStartTick) > 0) {
        _pendingReconcile = true;
        _reconcileStartTick = reconcileStartTick; // the +1 is important to include the faulty tick
      }
    }

    /// <summary> Requests the reconciliation process to start from a specific tick on an instance. </summary>
    /// <param name="reconcileStartTick">The tick from which to start reconciliation.</param>
    public void ReconcileFromTick(int reconcileStartTick)
      => RequestReconcileFromTick(reconcileStartTick);

    /// <summary> Retrieves the tick number from which reconciliation should start. </summary>
    /// <returns>The tick number from which to start reconciliation.</returns>
    public int GetReconcileStartTick() => _reconcileStartTick;

    /// <summary> Is reconcile requested or not </summary>
    public bool IsPEndingReconcile() => _pendingReconcile;

    /// <summary> Resets the reconciliation counter and pending flag, marking the reconciliation process as complete. </summary>
    public void ResetReconcile() {
      _reconcileStartTick = 0;
      _pendingReconcile = false;
    }
  }
}
