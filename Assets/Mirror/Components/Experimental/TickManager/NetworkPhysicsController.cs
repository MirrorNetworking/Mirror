using UnityEngine;
using System;

namespace Mirror.Components.Experimental{
  [DefaultExecutionOrder(-10)]
  [DisallowMultipleComponent]
  [AddComponentMenu("Network/Network Physics Controller")]
  public class NetworkPhysicsController : MonoBehaviour{
    /// <summary>
    /// Callback action to handle tick-forwarding logic.
    /// Allows external classes to define custom behavior when the tick advances.
    /// </summary>
    public Action<int> TickForwardCallback;

    private static int _reconcileStartTick = 0;

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
    public void RunSimulate(int deltaTicks) {
      var deltaTime = Time.fixedDeltaTime;
      for (var step = 0; step < deltaTicks; step++) {
        TickForward(1);
        NetworkPhysicsEntity.RunBeforeNetworkUpdates(1, deltaTime);
        NetworkPhysicsEntity.RunNetworkUpdates(1, deltaTime);
        PhysicsTick(deltaTime);
        NetworkPhysicsEntity.RunAfterNetworkUpdates(1, deltaTime);
      }
    }

    /// <summary>
    /// Runs the simulation for the specified number of delta ticks as a single batch.
    /// This method performs a single set of entity updates and a single physics tick
    /// scaled to account for the total number of ticks, useful for batching simulations.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks to simulate forward in one batch.</param>
    public void RunBatchSimulate(int deltaTicks) {
      var deltaTime = Time.fixedDeltaTime * deltaTicks;
      TickForward(deltaTicks);
      NetworkPhysicsEntity.RunBeforeNetworkUpdates(deltaTicks, deltaTime);
      NetworkPhysicsEntity.RunNetworkUpdates(deltaTicks, deltaTime);
      PhysicsTick(deltaTime); // Uses scaled deltaTime for batch processing
      NetworkPhysicsEntity.RunAfterNetworkUpdates(deltaTicks, deltaTime);
    }

    /// <summary>
    /// Requests the reconciliation process to start from a specific tick.
    /// Stores the earliest requested tick before reconciliation is executed.
    /// </summary>
    /// <param name="reconcileStartTick">The tick from which to start reconciliation.</param>
    public static void RequestReconcileFromTick(int reconcileStartTick) {
      if (_reconcileStartTick > reconcileStartTick || _reconcileStartTick == 0) {
        _reconcileStartTick = reconcileStartTick; // the +1 is important to include the faulty tick
      }
    }

    /// <summary>
    /// Retrieves the tick number from which reconciliation should start.
    /// </summary>
    /// <returns>The tick number from which to start reconciliation.</returns>
    public int GetReconcileStartTick() {
      return _reconcileStartTick;
    }

    /// <summary>
    /// Resets the reconciliation counter, marking the reconciliation process as complete.
    /// Sets _ticksToReconcile to 0, indicating no further reconciliation is required.
    /// </summary>
    public void ResetReconcile() {
      _reconcileStartTick = 0;
    }
  }
}