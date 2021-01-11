using UnityEngine;

namespace Mirror
{

    /// <summary>
    /// backing struct for a NetworkIdentity when used as a syncvar
    /// the weaver will replace the syncvar with this struct.
    /// </summary>
    public struct GameObjectSyncvar
    {
        /// <summary>
        /// The network client that spawned the parent object
        /// used to lookup the identity if it exists
        /// </summary>
        internal NetworkClient client;
        internal uint netId;

        internal GameObject gameObject;

        internal uint NetId => gameObject != null ? gameObject.GetComponent<NetworkIdentity>().NetId : netId;

        public GameObject Value
        {
            get
            {
                if (gameObject != null)
                    return gameObject;

                if (client != null)
                {
                    client.Spawned.TryGetValue(netId, out NetworkIdentity result);
                    if (result != null)
                        return result.gameObject;
                }

                return null;
            }

            set
            {
                if (value == null)
                    netId = 0;
                gameObject = value;
            }
        }
    }


    public static class GameObjectSerializers
    {
        public static void WriteGameObjectSyncVar(this NetworkWriter writer, GameObjectSyncvar id)
        {
            writer.WritePackedUInt32(id.NetId);
        }

        public static GameObjectSyncvar ReadGameObjectSyncVar(this NetworkReader reader)
        {
            uint netId = reader.ReadPackedUInt32();

            NetworkIdentity identity = null;
            if (!(reader.Client is null))
                reader.Client.Spawned.TryGetValue(netId, out identity);

            if (!(reader.Server is null))
                reader.Server.Spawned.TryGetValue(netId, out identity);


            return new GameObjectSyncvar
            {
                client = reader.Client,
                netId = netId,
                gameObject = identity != null ? identity.gameObject : null
            };
        }
    }
}