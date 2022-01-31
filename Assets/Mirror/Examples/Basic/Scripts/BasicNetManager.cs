using System;
using UnityEngine;

namespace Mirror.Examples.Basic
{
    [AddComponentMenu("")]
    public class BasicNetManager : NetworkManager
    {
        #region Logging

        void Log(string message, ConsoleColor consoleColor)
        {
#if UNITY_SERVER || UNITY_WEBGL
            Console.ForegroundColor = consoleColor;
            Console.WriteLine(message);
            Console.ResetColor();
#else
            Debug.Log(message);
#endif
        }

        #endregion

        #region Server

        public override void OnStartServer()
        {
            Log($"BasicNetManager::OnStartServer", ConsoleColor.Green);
            base.OnStartServer();
        }

        public override void OnServerConnect(NetworkConnection conn)
        {
            Log($"BasicNetManager::OnServerConnect {conn}", ConsoleColor.Green);
            base.OnServerConnect(conn);
        }

        public override void OnServerReady(NetworkConnection conn)
        {
            Log($"BasicNetManager::OnServerReady {conn}", ConsoleColor.Green);
            base.OnServerReady(conn);
        }

        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            Log($"BasicNetManager::OnServerAddPlayer {conn}", ConsoleColor.Green);
            base.OnServerAddPlayer(conn);
            Player.ResetPlayerNumbers();
        }

        public override void OnServerDisconnect(NetworkConnection conn)
        {
            Log($"BasicNetManager::OnServerDisconnect {conn}", ConsoleColor.Yellow);
            base.OnServerDisconnect(conn);
            Player.ResetPlayerNumbers();
        }

        public override void OnStopServer()
        {
            Log($"BasicNetManager::OnStopServer", ConsoleColor.Red);
            base.OnStopServer();
        }

        #endregion

        #region Client

        public override void OnStartClient()
        {
            Log($"BasicNetManager::OnStartClient", ConsoleColor.Green);
            base.OnStartClient();
        }

        public override void OnStopClient()
        {
            Log($"BasicNetManager::OnStopClient", ConsoleColor.Yellow);
            base.OnStopClient();
        }

        public override void OnClientConnect()
        {
            Log($"BasicNetManager::OnClientConnect", ConsoleColor.Green);
            base.OnClientConnect();
        }

        public override void OnClientDisconnect()
        {
            Log($"BasicNetManager::OnClientDisconnect", ConsoleColor.Yellow);
            base.OnClientDisconnect();
        }

        public override void OnClientNotReady()
        {
            Log($"BasicNetManager::OnClientNotReady", ConsoleColor.Red);
            base.OnClientNotReady();
        }

        #endregion
    }
}
