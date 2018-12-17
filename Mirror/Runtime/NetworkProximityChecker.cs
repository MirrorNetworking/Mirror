using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/NetworkProximityChecker")]
    [RequireComponent(typeof(NetworkIdentity))]
    public class NetworkProximityChecker : NetworkBehaviour
    {
        public enum CheckMethod
        {
            Physics3D,
            Physics2D
        }

        [TooltipAttribute("The maximum range that objects will be visible at.")]
        public int visRange = 10;

        [TooltipAttribute("How often (in seconds) that this object should update the set of players that can see it.")]
        public float visUpdateInterval = 1.0f; // in seconds

        [TooltipAttribute("Which method to use for checking proximity of players.\n\nPhysics3D uses 3D physics to determine proximity.\n\nPhysics2D uses 2D physics to determine proximity.")]
        public CheckMethod checkMethod = CheckMethod.Physics3D;

        [TooltipAttribute("Enable to force this object to be hidden from players.")]
        public bool forceHidden;

        [TooltipAttribute("Select only the Player's layer to avoid unnecessary SphereCasts against the Terrain, etc.")]
        public LayerMask castLayers = ~0; // ~0 means 'Everything'. layers are used anyway, might as well expose them to the user.

        float m_VisUpdateTime;

        void Update()
        {
            if (!NetworkServer.active)
                return;

            if (Time.time - m_VisUpdateTime > visUpdateInterval)
            {
                netIdentity.RebuildObservers(false);
                m_VisUpdateTime = Time.time;
            }
        }

        // called when a new player enters
        public override bool OnCheckObserver(NetworkConnection newObserver)
        {
            if (forceHidden)
                return false;

            if (newObserver.playerController != null)
            {
                return Vector3.Distance(newObserver.playerController.transform.position, transform.position) < visRange;
            }
            return false;
        }

        public override bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initial)
        {
            // only add self as observer if force hidden
            if (forceHidden)
            {
                // ensure player can still see themself
                if (connectionToClient != null)
                {
                    observers.Add(connectionToClient);
                }
            }
            // otherwise add everyone in proximity
            else
            {
                // find players within range
                switch (checkMethod)
                {
                    case CheckMethod.Physics3D:
                    {
                        Collider[] hits = Physics.OverlapSphere(transform.position, visRange, castLayers);
                        for (int i = 0; i < hits.Length; i++)
                        {
                            Collider hit = hits[i];
                            // collider might be on pelvis, often the NetworkIdentity is in a parent
                            // (looks in the object itself and then parents)
                            NetworkIdentity identity = hit.GetComponentInParent<NetworkIdentity>();
                            // (if an object has a connectionToClient, it is a player)
                            if (identity != null && identity.connectionToClient != null)
                            {
                                observers.Add(identity.connectionToClient);
                            }
                        }
                        break;
                    }

                    case CheckMethod.Physics2D:
                    {
                        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, visRange, castLayers);
                        for (int i = 0; i < hits.Length; i++)
                        {
                            Collider2D hit = hits[i];
                            // collider might be on pelvis, often the NetworkIdentity is in a parent
                            // (looks in the object itself and then parents)
                            NetworkIdentity identity = hit.GetComponentInParent<NetworkIdentity>();
                            // (if an object has a connectionToClient, it is a player)
                            if (identity != null && identity.connectionToClient != null)
                            {
                                observers.Add(identity.connectionToClient);
                            }
                        }
                        break;
                    }
                }
            }

            // always return true when overwriting OnRebuildObservers so that
            // Mirror knows not to use the built in rebuild method.
            return true;
        }

        // called hiding and showing objects on the host
        public override void OnSetLocalVisibility(bool visible)
        {
            foreach (Renderer rend in GetComponentsInChildren<Renderer>())
            {
                rend.enabled = visible;
            }
        }
    }
}
