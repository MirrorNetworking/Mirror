using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mirror;
using UnityEngine;

public class FastSpatialInterestManagement : InterestManagementBase
{
    Vector2Int InvalidPosition => new Vector2Int(int.MaxValue,
        int.MaxValue);


    [Tooltip("The maximum range that objects will be visible at.")]
    public int visRange = 30;

    [Tooltip("Rebuild all every 'rebuildInterval' seconds.")]
    public float rebuildInterval = 1;

    double lastRebuildTime;

    // we use a 9 neighbour grid.
    // so we always see in a distance of 2 grids.
    // for example, our own grid and then one on top / below / left / right.
    //
    // this means that grid resolution needs to be distance / 2.
    // so for example, for distance = 30 we see 2 cells = 15 * 2 distance.
    //
    // on first sight, it seems we need distance / 3 (we see left/us/right).
    // but that's not the case.
    // resolution would be 10, and we only see 1 cell far, so 10+10=20.
    int TileSize => visRange / 2;

    // the grid
    Dictionary<Vector2Int, HashSet<NetworkIdentity>> grid =
        new Dictionary<Vector2Int, HashSet<NetworkIdentity>>();

    class Tracked
    {
        public Vector2Int Position;
        public Transform Transform;
        public NetworkIdentity Identity;
        public Visibility PreviousVisibility;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2Int GridPosition(int tileSize)
        {
            Vector3 transformPos = Transform.position;
            return Vector2Int.RoundToInt(new Vector2(transformPos.x, transformPos.z) / tileSize);
        }
    }

    Dictionary<NetworkIdentity, Tracked> trackedIdentities = new Dictionary<NetworkIdentity, Tracked>();

    public override void Rebuild(NetworkIdentity identity, bool initialize)
    {
        // do nothing, we rebuild globally and individually in OnSpawned
    }

    public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)
    {
        // do nothing, we rebuild globally and individually in OnSpawned
        return false;
    }

    internal void LateUpdate()
    {
        // only on server
        if (!NetworkServer.active) return;

        // rebuild all spawned entities' observers every 'interval'
        // this will call OnRebuildObservers which then returns the
        // observers at grid[position] for each entity.
        if (NetworkTime.localTime >= lastRebuildTime + rebuildInterval)
        {
            RebuildAll();
            lastRebuildTime = NetworkTime.localTime;
        }
    }

    // When a new identity is spawned
    public override void OnSpawned(NetworkIdentity identity)
    {
        // host visibility shim to make sure unseen entities are hidden, we initialize actual visibility later
        if (NetworkClient.active)
        {
            SetHostVisibility(identity, false);
        }

        if (identity.connectionToClient != null)
        {
            // client always sees itself
            AddObserver(identity.connectionToClient, identity);
        }

        Tracked tracked = new Tracked
        {
            Transform = identity.transform, Identity = identity, PreviousVisibility = identity.visibility,
        };

        // set initial position
        tracked.Position = tracked.GridPosition(TileSize);
        // add to tracked
        trackedIdentities.Add(identity, tracked);
        // initialize in grid
        RebuildAdd(identity, InvalidPosition, tracked.Position, true);
    }


    // when an identity is despawned/destroyed
    public override void OnDestroyed(NetworkIdentity identity)
    {
        Tracked obj = trackedIdentities[identity];
        trackedIdentities.Remove(identity);

        // observers are cleaned up automatically when destroying, we just need to remove it from our grid
        grid[obj.Position].Remove(identity);
    }

    private void RebuildAll()
    {
        // loop over all identities and check if their position has changed
        foreach (Tracked tracked in trackedIdentities.Values)
        {
            // Check if visibility has changed, this should usually be false
            bool visibilityChanged = tracked.Identity.visibility != tracked.PreviousVisibility;
            // Visibility change *to* default needs to be handled before the normal grid update
            // since observers are manipulated in RebuildAdd/RebuildRemove if visibility == Default
            if (visibilityChanged && tracked.Identity.visibility == Visibility.Default)
            {
                switch (tracked.PreviousVisibility)
                {
                    case Visibility.ForceHidden:
                        // Hidden To Default
                        AddObserversHiddenToDefault(tracked.Identity, tracked.Position);
                        break;
                    case Visibility.ForceShown:
                        // Shown To Default
                        RemoveObserversShownToDefault(tracked.Identity, tracked.Position);
                        break;
                    case Visibility.Default:
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            Vector2Int currentPosition = tracked.GridPosition(TileSize);
            // if the position changed, move the identity in the grid and update observers accordingly
            if (currentPosition != tracked.Position)
            {
                Vector2Int oldPosition = tracked.Position;
                tracked.Position = currentPosition;
                // First remove from old grid position
                RebuildRemove(tracked.Identity, oldPosition, currentPosition);
                // Then add to new grid position
                RebuildAdd(tracked.Identity, oldPosition, currentPosition, false);
            }

            // after updating the grid, if the visibility has changed
            if (visibilityChanged)
            {
                switch (tracked.Identity.visibility)
                {
                    case Visibility.ForceHidden:
                        ClearObservers(tracked.Identity);
                        break;
                    case Visibility.ForceShown:
                        AddObserversAllReady(tracked.Identity);
                        break;
                    case Visibility.Default:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                tracked.PreviousVisibility = tracked.Identity.visibility;
            }
        }
    }

    private void RebuildRemove(NetworkIdentity changedIdentity, Vector2Int oldPosition, Vector2Int newPosition)
    {
        // sanity check
        if (!grid[oldPosition].Remove(changedIdentity))
        {
            throw new InvalidOperationException("changedIdentity was not in the provided grid");
        }

        // for all tiles the changedIdentity could see at the old position
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int tilePos = oldPosition + new Vector2Int(x, y);
                // Skip grid tiles that are still visible
                if (Mathf.Abs(tilePos.x - newPosition.x) <= 1 &&
                    Mathf.Abs(tilePos.y - newPosition.y) <= 1)
                {
                    continue;
                }

                if (!grid.TryGetValue(tilePos, out HashSet<NetworkIdentity> tile))
                {
                    continue;
                }

                foreach (NetworkIdentity gridIdentity in tile)
                {
                    if (gridIdentity == changedIdentity)
                    {
                        // Don't do anything with yourself
                        continue;
                    }

                    // we only modify observers here if the visibility is default, ForceShown/ForceHidden are handled in RebuildAll

                    // if the gridIdentity is a player, it can't see changedIdentity anymore
                    if (gridIdentity.connectionToClient != null && changedIdentity.visibility == Visibility.Default)
                    {
                        RemoveObserver(gridIdentity.connectionToClient, changedIdentity);
                    }

                    // if the changedIdentity is a player, it can't see gridIdentity anymore
                    if (changedIdentity.connectionToClient != null && gridIdentity.visibility == Visibility.Default)
                    {
                        RemoveObserver(changedIdentity.connectionToClient, gridIdentity);
                    }
                }
            }
        }
    }

    private void RebuildAdd(NetworkIdentity changedIdentity, Vector2Int oldPosition, Vector2Int newPosition,
        bool initialize)
    {
        // for all tiles the changedIdentity now sees at the new position
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int tilePos = newPosition + new Vector2Int(x, y);

                // Skip grid tiles that were already visible before moving
                if (!initialize && (Mathf.Abs(tilePos.x - oldPosition.x) <= 1 &&
                                    Mathf.Abs(tilePos.y - oldPosition.y) <= 1))
                {
                    continue;
                }

                if (!grid.TryGetValue(tilePos, out HashSet<NetworkIdentity> tile))
                {
                    continue;
                }

                foreach (NetworkIdentity gridIdentity in tile)
                {
                    if (gridIdentity == changedIdentity)
                    {
                        // Don't do anything with yourself
                        continue;
                    }

                    // we only modify observers here if the visibility is default, ForceShown/ForceHidden are handled in RebuildAll

                    // if the gridIdentity is a player, it can now see changedIdentity
                    if (gridIdentity.connectionToClient != null && changedIdentity.visibility == Visibility.Default)
                    {
                        AddObserver(gridIdentity.connectionToClient, changedIdentity);
                    }

                    // if the changedIdentity is a player, it can now see gridIdentity
                    if (changedIdentity.connectionToClient != null && gridIdentity.visibility == Visibility.Default)
                    {
                        AddObserver(changedIdentity.connectionToClient, gridIdentity);
                    }
                }
            }
        }

        // add ourselves to the new grid position
        if (!grid.TryGetValue(newPosition, out HashSet<NetworkIdentity> addTile))
        {
            addTile = new HashSet<NetworkIdentity>();
            grid[newPosition] = addTile;
        }

        if (!addTile.Add(changedIdentity))
        {
            throw new InvalidOperationException("identity was already in the grid");
        }
    }

    /// Adds observers to the NI, but not the other way around. This is used when a NI changes from ForceHidden to Default
    private void AddObserversHiddenToDefault(NetworkIdentity changed, Vector2Int gridPosition)
    {
        // for all tiles around the changedIdentity
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int tilePos = gridPosition + new Vector2Int(x, y);
                if (!grid.TryGetValue(tilePos, out HashSet<NetworkIdentity> tile))
                {
                    continue;
                }

                foreach (NetworkIdentity gridIdentity in tile)
                {
                    if (gridIdentity == changed)
                    {
                        // Don't do anything with yourself
                        continue;
                    }

                    // if the gridIdentity is a player, it can now see changedIdentity
                    if (gridIdentity.connectionToClient != null)
                    {
                        AddObserver(gridIdentity.connectionToClient, changed);
                    }
                }
            }
        }
    }

    // Temp hashset to avoid runtime allocation
    private HashSet<NetworkConnectionToClient> tempShownToDefaultSet = new HashSet<NetworkConnectionToClient>();

    /// Removes observers from the NI, but doesn't change observing. This is used when a NI changes from ForceShown to Default
    private void RemoveObserversShownToDefault(NetworkIdentity changedIdentity, Vector2Int gridPosition)
    {
        tempShownToDefaultSet.Clear();
        // copy over all current connections that are seeing the NI
        foreach (NetworkConnectionToClient observer in changedIdentity.observers.Values)
        {
            tempShownToDefaultSet.Add(observer);
        }

        // for all tiles around the changedIdentity, remove any connections that can still see it
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int tilePos = gridPosition + new Vector2Int(x, y);
                if (!grid.TryGetValue(tilePos, out HashSet<NetworkIdentity> tile))
                {
                    continue;
                }

                foreach (NetworkIdentity gridIdentity in tile)
                {
                    // if the gridIdentity is a player, it can see changedIdentity
                    // (also yourself! don't need the extra check here)
                    if (gridIdentity.connectionToClient != null)
                    {
                        tempShownToDefaultSet.Remove(gridIdentity.connectionToClient);
                    }
                }
            }
        }

        // any left over connections can't see changedIdentity - thus need removing
        foreach (NetworkConnectionToClient connection in tempShownToDefaultSet)
        {
            RemoveObserver(connection, changedIdentity);
        }

        // clear when done
        tempShownToDefaultSet.Clear();
    }
}
