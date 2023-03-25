// interest management component for custom solutions like
// distance based, spatial hashing, raycast based, etc.
// low level base class allows for low level spatial hashing etc., which is 3-5x faster.
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [HelpURL("https://mirror-networking.gitbook.io/docs/guides/interest-management")]
    public abstract class InterestManagementBase : MonoBehaviour
    {
        // Configures InterestManagementBase in NetworkServer/Client
        // Do NOT check for active server or client here.
        // OnEnable must always set the static aoi references.
        // make sure to call base.OnEnable when overwriting!
        // Previously used Awake()
        protected virtual void OnEnable()
        {
            if (NetworkServer.aoi == null)
            {
                NetworkServer.aoi = this;
            }
            else Debug.LogError($"Only one InterestManagement component allowed. {NetworkServer.aoi.GetType()} has been set up already.");

            if (NetworkClient.aoi == null)
            {
                NetworkClient.aoi = this;
            }
            else Debug.LogError($"Only one InterestManagement component allowed. {NetworkClient.aoi.GetType()} has been set up already.");
        }

        [ServerCallback]
        public virtual void Reset() {}

        // Callback used by the visibility system to determine if an observer
        // (player) can see the NetworkIdentity. If this function returns true,
        // the network connection will be added as an observer.
        //   conn: Network connection of a player.
        //   returns True if the player can see this object.
        public abstract bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver);


        // Callback used by the visibility system for objects on a host.
        // Objects on a host (with a local client) cannot be disabled or
        // destroyed when they are not visible to the local client. So this
        // function is called to allow custom code to hide these objects. A
        // typical implementation will disable renderer components on the
        // object. This is only called on local clients on a host.
        // => need the function in here and virtual so people can overwrite!
        // => not everyone wants to hide renderers!
        [ServerCallback]
        public virtual void SetHostVisibility(NetworkIdentity identity, bool visible)
        {
            foreach (Renderer rend in identity.GetComponentsInChildren<Renderer>())
                rend.enabled = visible;
        }

        /// <summary>Called on the server when a new networked object is spawned.</summary>
        // (useful for 'only rebuild if changed' interest management algorithms)
        [ServerCallback]
        public virtual void OnSpawned(NetworkIdentity identity) {}

        /// <summary>Called on the server when a networked object is destroyed.</summary>
        // (useful for 'only rebuild if changed' interest management algorithms)
        [ServerCallback]
        public virtual void OnDestroyed(NetworkIdentity identity) {}

        public abstract void Rebuild(NetworkIdentity identity, bool initialize);

        /// <summary>Adds the specified connection to the observers of identity</summary>
        protected void AddObserver(NetworkConnectionToClient connection, NetworkIdentity identity)
        {
            connection.AddToObserving(identity);
            identity.observers.Add(connection.connectionId, connection);
        }

        /// <summary>Removes the specified connection from the observers of identity</summary>
        protected void RemoveObserver(NetworkConnectionToClient connection, NetworkIdentity identity)
        {
            connection.RemoveFromObserving(identity, false);
            identity.observers.Remove(connection.connectionId);
        }
    }
}
