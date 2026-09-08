using System.Collections.Generic;

namespace Mirror.Components.Experimental{
  /// <summary>
  /// Interface representing a network item that requires updates at various stages of the network tick cycle.
  /// Each method in this interface is intended to handle specific stages of the update process.
  /// </summary>
  public interface INetworkedItem{
    /// <summary>
    /// Called when client and server are synchronized.
    /// </summary>
    void OnNetworkSynchronized();

    /// <summary>
    /// Called before the network reconciliation process begins, allowing the item to properly reset state to the last known good state.
    /// </summary>
    void OnResetNetworkState();

    /// <summary>
    /// Called after the network reconciliation process ends.
    /// </summary>
    void AfterNetworkReconcile() {
    }

    /// <summary>
    /// Called before the main network update, allowing the item to perform any necessary preparation or pre-update logic.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks since the last update.</param>
    /// <param name="deltaTime">The time elapsed since the last update in seconds.</param>
    void OnBeforeNetworkUpdate(int deltaTicks, float deltaTime) {
    }

    /// <summary>
    /// Called during the main network update, allowing the item to handle core updates related to network state, physics, or entity positioning.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks since the last update.</param>
    /// <param name="deltaTime">The time elapsed since the last update in seconds.</param>
    void OnNetworkUpdate(int deltaTicks, float deltaTime);

    /// <summary>
    /// Called after the main network update, allowing the item to perform any necessary cleanup or post-update logic.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks since the last update.</param>
    /// <param name="deltaTime">The time elapsed since the last update in seconds.</param>
    void OnAfterNetworkUpdate(int deltaTicks, float deltaTime) {
    }
  }

  /// <summary>
  /// Manages network update sequences for entities requiring tick-based adjustments.
  /// </summary>
  public class NetworkPhysicsEntity{
    /// <summary> Stores items requiring updates on each tick, as a list of tuples with priority and item. </summary>
    private static readonly List<(int priority, INetworkedItem item)> NetworkItems = new List<(int, INetworkedItem)>();

    /// <summary> Adds a network entity to the collection for updates and sorts by priority. </summary>
    /// <param name="item">The network item implementing <see cref="INetworkedItem"/> that requires tick updates.</param>
    /// <param name="priority">The priority for the entity, with lower numbers indicating higher priority.</param>
    public static void AddNetworkEntity(INetworkedItem item, int priority = 0) {
      // Add item to list of executables
      NetworkItems.Add((priority, item));

      // Fortunately, List.Sort() in C# uses a stable sorting algorithm so same priority remains in the same order and new items are added to the end
      // [2-a, 1-a, 1-b, 0-a, 0-b, 0-c] + [1-c] => [2-a, 1-a, 1-b, 1-c, 0-a, 0-b, 0-c]
      NetworkItems.Sort((x, y) => y.priority.CompareTo(x.priority));
    }

    /// <summary> Removes a network entity from the collection based on the item reference only. </summary>
    /// <param name="item">The network item to remove.</param>
    public static void RemoveNetworkEntity(INetworkedItem item) {
      NetworkItems.RemoveAll(entry => entry.item.Equals(item));
    }

    /// <summary>
    /// Runs the AfterReconcile method on each network item in priority order.
    /// This method is intended to signal reconcile complete.
    /// </summary>
    public void RunAfterReconcile() {
      foreach (var (_, item) in NetworkItems) {
        item.AfterNetworkReconcile();
      }
    }

    /// <summary>
    /// Runs the OnResetNetworkState method on each network item in priority order.
    /// This method is intended to reset the network state before any updates are processed.
    /// </summary>
    public void RunResetNetworkState() {
      foreach (var (_, item) in NetworkItems) {
        item.OnResetNetworkState();
      }
    }

    /// <summary>
    /// Runs the OnNetworkSynchronized method on each network item in priority order.
    /// This method is intended to signal that the network state is synchronized.
    /// </summary>
    public void RunNetworkSynchronized() {
      foreach (var (_, item) in NetworkItems) {
        item.OnNetworkSynchronized();
      }
    }

    /// <summary>
    /// Runs the OnBeforeNetworkUpdate method on each network item in priority order.
    /// This method is intended to perform any necessary setup or pre-update logic before the main network updates are processed.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks since the last update.</param>
    /// <param name="deltaTime">The time elapsed since the last update in seconds.</param>
    public void RunBeforeNetworkUpdates(int deltaTicks, float deltaTime) {
      foreach (var (_, item) in NetworkItems) {
        item.OnBeforeNetworkUpdate(deltaTicks, deltaTime);
      }
    }

    /// <summary>
    /// Runs the OnNetworkUpdate method on each network item in priority order.
    /// This method executes the main network update logic for each item, handling any core updates needed for the network state or entity positions.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks since the last update.</param>
    /// <param name="deltaTime">The time elapsed since the last update in seconds.</param>
    public void RunNetworkUpdates(int deltaTicks, float deltaTime) {
      foreach (var (_, item) in NetworkItems) {
        item.OnNetworkUpdate(deltaTicks, deltaTime);
      }
    }

    /// <summary>
    /// Runs the AfterNetworkUpdate method on each network item in priority order.
    /// This method is intended for any necessary cleanup or post-update logic following the main network updates.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks since the last update.</param>
    /// <param name="deltaTime">The time elapsed since the last update in seconds.</param>
    public void RunAfterNetworkUpdates(int deltaTicks, float deltaTime) {
      foreach (var (_, item) in NetworkItems) {
        item.OnAfterNetworkUpdate(deltaTicks, deltaTime);
      }
    }
  }
}
