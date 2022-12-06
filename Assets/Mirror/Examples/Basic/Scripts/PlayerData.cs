using UnityEngine;
using Mirror;
using Mirror.Examples.Basic;

/*
	Documentation: https://mirror-networking.gitbook.io/docs/guides/networkbehaviour
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkBehaviour.html
*/

// NOTE: Do not put objects in DontDestroyOnLoad (DDOL) in Awake.  You can do that in Start instead.

public class PlayerData : NetworkBehaviour
{
    public event System.Action<ushort> OnPlayerDataChanged;

    /// <summary>
    /// This is updated by UpdateData which is called from OnStartServer via InvokeRepeating
    /// </summary>
    [SyncVar(hook = nameof(PlayerDataChanged))]
    public ushort playerData = 0;

    [Header("Set At Runtime by Player")]
    public PlayerUI playerUI = null;

    // This only runs on the client, called from OnStartClient via InvokeRepeating
    [ClientCallback]
    void UpdateData()
    {
        playerData = (ushort)Random.Range(100, 1000);
    }

    // This is called by the hook of playerData SyncVar above
    void PlayerDataChanged(ushort _, ushort newPlayerData)
    {
        Debug.Log($"PlayerDataChanged SyncVar Hook {(playerUI == null ? string.Empty : playerUI.playerNameText.text)} {newPlayerData}", gameObject);
        OnPlayerDataChanged?.Invoke(newPlayerData);
    }

    #region Start & Stop Callbacks

        /// <summary>
        /// Called on every NetworkBehaviour when it is activated on a client.
        /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
        /// </summary>
    public override void OnStartClient()
    {
        // Wire up event to handler in PlayerUI
        OnPlayerDataChanged = playerUI.OnPlayerDataChanged;
    }

    /// <summary>
    /// Called when the local player object has been set up.
    /// <para>This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.</para>
    /// </summary>
    public override void OnStartLocalPlayer() 
    {
        // Set isLocalPlayer for this Player in UI for background shading
        playerUI.SetLocalPlayer();

        // Start generating updates
        InvokeRepeating(nameof(UpdateData), 1, 1);
    }

    /// <summary>
    /// This is invoked on clients when the server has caused this object to be destroyed.
    /// <para>This can be used as a hook to invoke effects or do client specific cleanup.</para>
    /// </summary>
    public override void OnStopClient() 
    {
        CancelInvoke();
        OnPlayerDataChanged = null;
    }

    #endregion
}
