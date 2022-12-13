using UnityEngine;
using Mirror;
using Mirror.Examples.Basic;

public class PlayerData : NetworkBehaviour
{
    public event System.Action<ushort> OnPlayerDataChanged;

    [Tooltip("This is updated by UpdateData which is called from OnStartLocalPlayer via InvokeRepeating")]
    [SyncVar(hook = nameof(PlayerDataChanged))]
    public ushort playerData = 0;

    [Header("Set At Runtime by Player")]
    public PlayerUI playerUI = null;

    // This only runs on local player, called from OnStartLocalPlayer via InvokeRepeating
    [ClientCallback]
    void UpdateData()
    {
        // Update SyncVar with Client-to-Server Sync Direction
        playerData = (ushort)Random.Range(100, 1000);

        // Hook doesn't fire for owner with Client-to-Server Sync Direction
        // so we must invoke the action directly, if we're not the host client
        if (isClientOnly)
            OnPlayerDataChanged?.Invoke(playerData);
    }

    // This is called by the hook of playerData SyncVar above
    void PlayerDataChanged(ushort _, ushort newPlayerData)
    {
        //Debug.Log($"PlayerDataChanged SyncVar Hook {(playerUI == null ? string.Empty : playerUI.playerNameText.text)} {newPlayerData}", gameObject);
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
