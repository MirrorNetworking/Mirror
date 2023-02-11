using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class FastSpatialInterestManagement : InterestManagementBase {
    [Tooltip("The maximum range that objects will be visible at.")]
    public int visRange = 30;

    private int TileSize => visRange / 3;

    // the grid
    private Dictionary<Vector2Int, HashSet<NetworkIdentity>> grid =
        new Dictionary<Vector2Int, HashSet<NetworkIdentity>>();

    class Tracked {
        public bool uninitialized;
        public Vector2Int position;
        public Transform transform;
        public NetworkIdentity identity;
    }

    private Dictionary<NetworkIdentity, Tracked> tracked = new Dictionary<NetworkIdentity, Tracked>();

    public override void Rebuild(NetworkIdentity identity, bool initialize) {
        // do nothing, we update every frame.
    }

    public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver) {
        // we build initial state during the normal loop too
        return false;
    }

    // update everyone's position in the grid
    internal void LateUpdate() {
        // only on server
        if (!NetworkServer.active) return;

        RebuildAll();
    }

    // When a new entity is spawned
    public override void OnSpawned(NetworkIdentity identity) {
        // (limitation: we never expect identity.visibile to change)
        if (identity.visible != Visibility.Default) {
            return;
        }

        // host visibility shim to make sure unseen entities are hidden
        if (NetworkClient.active) {
            SetHostVisibility(identity, false);
        }

        if (identity.connectionToClient != null) {
            // client always sees itself
            AddObserver(identity.connectionToClient, identity);
        }

        tracked.Add(identity, new Tracked {
            uninitialized = true,
            position = new Vector2Int(int.MaxValue, int.MaxValue), // invalid
            transform = identity.transform,
            identity = identity,
        });
    }

    // when an entity is despawned/destroyed
    public override void OnDestroyed(NetworkIdentity identity) {
        // (limitation: we never expect identity.visibile to change)
        if (identity.visible != Visibility.Default) {
            return;
        }

        var obj = tracked[identity];
        tracked.Remove(identity);

        if (!obj.uninitialized) {
            // observers are cleaned up automatically when destroying, we just need to remove it from our grid
            grid[obj.position].Remove(identity);
        }
    }

    private void RebuildAll() {
        // loop over all entities and check if their positions changed
        foreach (var trackedEntity in tracked.Values) {
            Vector2Int pos =
                Vector2Int.RoundToInt(
                    new Vector2(trackedEntity.transform.position.x, trackedEntity.transform.position.z) / TileSize);
            if (pos != trackedEntity.position) {
                // if the position changed, move entity about
                Vector2Int oldPos = trackedEntity.position;
                trackedEntity.position = pos;
                // First: Remove from old grid position, but only if it was ever in the grid
                if (!trackedEntity.uninitialized) {
                    RebuildRemove(trackedEntity.identity, oldPos, pos);
                }

                RebuildAdd(trackedEntity.identity, oldPos, pos, trackedEntity.uninitialized);
                trackedEntity.uninitialized = false;
            }
        }
    }

    private void RebuildRemove(NetworkIdentity entity, Vector2Int oldPosition, Vector2Int newPosition) {
        // sanity check
        if (!grid[oldPosition].Remove(entity)) {
            throw new InvalidOperationException("entity was not in the provided grid");
        }

        // for all tiles the entity could see at the old position
        for (int x = -1; x <= 1; x++) {
            for (int y = -1; y <= 1; y++) {
                var tilePos = oldPosition + new Vector2Int(x, y);
                // optimization: don't remove on overlapping tiles
                if (Mathf.Abs(tilePos.x - newPosition.x) <= 1 &&
                    Mathf.Abs(tilePos.y - newPosition.y) <= 1) {
                    continue;
                }

                if (!grid.TryGetValue(tilePos, out HashSet<NetworkIdentity> tile)) {
                    continue;
                }

                // update observers for all identites the entity could see and all players that could see it
                foreach (NetworkIdentity identity in tile) {
                    // dont touch yourself (hah.)
                    if (identity == entity) {
                        continue;
                    }

                    // if the identity is a player, remove the entity from it
                    if (identity.connectionToClient != null) {
                        RemoveObserver(identity.connectionToClient, entity);
                    }

                    // if the entity is a player, remove the identity from it
                    if (entity.connectionToClient != null) {
                        RemoveObserver(entity.connectionToClient, identity);
                    }
                }
            }
        }
    }

    private void RebuildAdd(NetworkIdentity entity, Vector2Int oldPos, Vector2Int newPos, bool initialize) {
        // for all tiles the entity now sees at the new position
        for (int x = -1; x <= 1; x++) {
            for (int y = -1; y <= 1; y++) {
                var tilePos = newPos + new Vector2Int(x, y);
                // optimization: don't add on overlapping tiles
                if (!initialize && (Mathf.Abs(tilePos.x - oldPos.x) <= 1 &&
                                    Mathf.Abs(tilePos.y - oldPos.y) <= 1)) {
                    continue;
                }

                if (!grid.TryGetValue(tilePos, out var tile)) {
                    continue;
                }

                foreach (var identity in tile) {
                    // dont touch yourself (hah.)
                    if (identity == entity) {
                        continue;
                    }

                    // if the identity is a player, add the entity to it
                    if (identity.connectionToClient != null) {
                        try {
                            AddObserver(identity.connectionToClient, entity);
                        } catch (ArgumentException e) {
                            // sanity check
                            Debug.LogError(
                                $"Failed to add {entity} (#{entity.netId}) to the observers of {identity} (#{identity.netId}) (case 1)\n{e}");
                        }
                    }

                    // if the entity is a player, add the identity to it
                    if (entity.connectionToClient != null) {
                        try {
                            AddObserver(entity.connectionToClient, identity);
                        } catch (ArgumentException e) {
                            // sanity check
                            Debug.LogError(
                                $"Failed to add {identity} (#{identity.netId}) to the observers of {entity} (#{entity.netId}) (case 2)\n{e}");
                        }
                    }
                }
            }
        }

        // add ourselves to the new grid position
        if (!grid.TryGetValue(newPos, out HashSet<NetworkIdentity> addTile)) {
            addTile = new HashSet<NetworkIdentity>();
            grid[newPos] = addTile;
        }

        if (!addTile.Add(entity)) {
            throw new InvalidOperationException("entity was already in the grid");
        }
    }
}
