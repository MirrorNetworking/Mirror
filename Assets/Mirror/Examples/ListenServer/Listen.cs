// add this component to the NetworkManager
using UnityEngine;

namespace Mirror.Examples.Listen
{
    public struct ServerInfo
    {
        public string ip;
        public ushort port;
        public string title;
        public ushort players;
        public ushort capacity;
    }

    [RequireComponent(typeof(NetworkManager))]
    public class Listen : MonoBehaviour
    {
        // game server to listen server connection

        // client to listen server connection
        // (only active while receiving game server lists)

        public Rect window = new Rect(5, 150, 400, 400);

        void Update()
        {
            // TODO send server data to listen
        }

        void OnGUI()
        {
            // TODO show listen data on client
            if (!NetworkManager.IsHeadless() && !NetworkServer.active && !NetworkClient.active)
            {
                GUILayout.BeginArea(window);
                GUILayout.BeginVertical("box");

                // header
                GUILayout.Label("Gameserver List:");



                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }
    }
}
