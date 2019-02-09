using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

public class NetworkLobbyPlayerExt : NetworkLobbyPlayer
{
    public string Name;
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (LogFilter.Debug) Debug.LogFormat("OnStartClient {0}", SceneManager.GetActiveScene().name);
        NetworkLobbyManager lobby = NetworkManager.singleton as NetworkLobbyManager;
        if (lobby && lobby.LobbyScene == SceneManager.GetActiveScene().name)
            gameObject.transform.parent = GameObject.Find("Players").transform;
    }

    public override void OnClientEnterLobby()
    {
        Debug.LogFormat("OnClientEnterLobby {0}", SceneManager.GetActiveScene().name);
    }

    public override void OnClientExitLobby()
    {
        Debug.LogFormat("OnClientExitLobby {0}", SceneManager.GetActiveScene().name);
    }
}
