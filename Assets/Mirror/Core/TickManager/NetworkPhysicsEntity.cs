using System.Collections.Generic;

namespace Mirror{
  /// <summary>
  /// Interface representing a network item that requires updates at various stages of the network tick cycle.
  /// Each method in this interface is intended to handle specific stages of the update process.
  /// </summary>
  public interface INetworkedItem{
    /// <summary>
    /// Called before the main network update, allowing the item to perform any necessary preparation or pre-update logic.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks since the last update.</param>
    /// <param name="deltaTime">The time elapsed since the last update in seconds.</param>
    void BeforeNetworkUpdate(int deltaTicks, float deltaTime);

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
    void AfterNetworkUpdate(int deltaTicks, float deltaTime);
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
      NetworkItems.Add((priority, item));
      NetworkItems.Sort((x, y) => y.priority.CompareTo(x.priority));
      // Fortunately, List.Sort() in C# uses a stable sorting algorithm so same priority remains in the same order and new items are added to the end
      // [2-a, 1-a, 1-b, 0-a, 0-b, 0-c] + [1-c] => [2-a, 1-a, 1-b, 1-c, 0-a, 0-b, 0-c]
    }

    /// <summary> Removes a network entity from the collection based on the item reference only. </summary>
    /// <param name="item">The network item to remove.</param>
    public static void RemoveNetworkEntity(INetworkedItem item) {
      NetworkItems.RemoveAll(entry => entry.item.Equals(item));
    }


    /// <summary>
    /// Runs the BeforeNetworkUpdate method on each network item in priority order.
    /// This method is intended to perform any necessary setup or pre-update logic before the main network updates are processed.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks since the last update.</param>
    /// <param name="deltaTime">The time elapsed since the last update in seconds.</param>
    public static void RunBeforeNetworkUpdates(int deltaTicks, float deltaTime) {
      foreach (var (priority, item) in NetworkItems) {
        item.BeforeNetworkUpdate(deltaTicks, deltaTime);
      }
    }


    /// <summary>
    /// Runs the OnNetworkUpdate method on each network item in priority order.
    /// This method executes the main network update logic for each item, handling any core updates needed for the network state or entity positions.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks since the last update.</param>
    /// <param name="deltaTime">The time elapsed since the last update in seconds.</param>
    public static void RunNetworkUpdates(int deltaTicks, float deltaTime) {
      foreach (var (priority, item) in NetworkItems) {
        item.OnNetworkUpdate(deltaTicks, deltaTime);
      }
    }


    /// <summary>
    /// Runs the AfterNetworkUpdate method on each network item in priority order.
    /// This method is intended for any necessary cleanup or post-update logic following the main network updates.
    /// </summary>
    /// <param name="deltaTicks">The number of ticks since the last update.</param>
    /// <param name="deltaTime">The time elapsed since the last update in seconds.</param>
    public static void RunAfterNetworkUpdates(int deltaTicks, float deltaTime) {
      foreach (var (priority, item) in NetworkItems) {
        item.AfterNetworkUpdate(deltaTicks, deltaTime);
      }
    }
  }
}