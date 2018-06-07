#if ENABLE_UNET
using System;
using System.Collections.Generic;

namespace UnityEngine.Networking
{
    // This has a list of real connections
    // The local or "fake" connections are kept separate because sometimes you
    // only want to iterate through those, and not all connections.
    class ConnectionArray
    {
        List<NetworkConnection> m_LocalConnections;
        List<NetworkConnection> m_Connections;

        internal List<NetworkConnection> localConnections { get { return m_LocalConnections; }}
        internal List<NetworkConnection> connections { get { return m_Connections; }}

        public int Count { get { return m_Connections.Count; } }

        public int LocalIndex { get { return -m_LocalConnections.Count; } }

        public ConnectionArray()
        {
            m_Connections = new List<NetworkConnection>();
            m_LocalConnections = new List<NetworkConnection>();
        }

        public int Add(int connId, NetworkConnection conn)
        {
            if (connId < 0)
            {
                if (LogFilter.logWarn) {Debug.LogWarning("ConnectionArray Add bad id " + connId); }
                return -1;
            }

            if (connId < m_Connections.Count && m_Connections[connId] != null)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("ConnectionArray Add dupe at " + connId); }
                return -1;
            }

            while (connId > (m_Connections.Count - 1))
            {
                m_Connections.Add(null);
            }

            m_Connections[connId] = conn;
            return connId;
        }

        // call this if you know the connnection exists
        public NetworkConnection Get(int connId)
        {
            if (connId < 0)
            {
                return m_LocalConnections[Mathf.Abs(connId) - 1];
            }

            if (connId < 0 || connId > m_Connections.Count)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("ConnectionArray Get invalid index " + connId); }
                return null;
            }

            return m_Connections[connId];
        }

        // call this if the connection may not exist (in disconnect handler)
        public NetworkConnection GetUnsafe(int connId)
        {
            if (connId < 0 || connId > m_Connections.Count)
            {
                return null;
            }
            return m_Connections[connId];
        }

        public void Remove(int connId)
        {
            if (connId < 0)
            {
                m_LocalConnections[Mathf.Abs(connId) - 1] = null;
                return;
            }

            if (connId < 0 || connId > m_Connections.Count)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("ConnectionArray Remove invalid index " + connId); }
                return;
            }
            m_Connections[connId] = null;
        }

        public int AddLocal(NetworkConnection conn)
        {
            m_LocalConnections.Add(conn);
            int index = -m_LocalConnections.Count;
            conn.connectionId = index;
            return index;
        }

        public bool ContainsPlayer(GameObject player, out NetworkConnection conn)
        {
            conn = null;
            if (player == null)
                return false;

            for (int i = LocalIndex; i < m_Connections.Count; i++)
            {
                conn = Get(i);
                if (conn != null)
                {
                    for (int j = 0; j < conn.playerControllers.Count; j++)
                    {
                        if (conn.playerControllers[j].IsValid && conn.playerControllers[j].gameObject == player)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
#endif //ENABLE_UNET
