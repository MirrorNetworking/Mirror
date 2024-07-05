using System.Collections.Generic;
using UnityEngine;
using Mirror.Examples.Common.Controllers.Tank;
using Mirror.Examples.Common;
using System.Collections;

namespace Mirror.Examples.TankTheftAuto
{
    public class TankAuthority : NetworkBehaviour
    {
        [Header("Components")]
        public GameObject triggerUI;
        public TankTurret tankTurret;
        public GameObject tankTrigger;

        [SyncVar(hook = nameof(OnIsControlledChanged))]
        public bool isControlled;

        void OnIsControlledChanged(bool _, bool newValue)
        {
            tankTrigger.SetActive(!newValue);
        }

        protected override void OnValidate()
        {
            if (Application.isPlaying) return;

            base.OnValidate();
            Reset();
        }

        void Reset()
        {
            if (triggerUI == null)
                triggerUI = transform.Find("TriggerUI").gameObject;

            if (tankTrigger == null)
                tankTrigger = transform.Find("TankTrigger").gameObject;

            if (tankTurret == null)
                tankTurret = GetComponent<TankTurret>();

            triggerUI.SetActive(false);
        }

        [ClientCallback]
        void Update()
        {
            if (triggerUI.activeSelf && Input.GetKeyDown(KeyCode.C))
                CmdTakeControl();

            if (isOwned && Input.GetKeyDown(KeyCode.X))
                CmdReleaseControl();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!isClient || !other.gameObject.CompareTag("Player")) return;

            if (other.TryGetComponent(out NetworkIdentity networkIdentity))
                if (networkIdentity == NetworkClient.localPlayer)
                    triggerUI.SetActive(true);
        }

        void OnTriggerExit(Collider other)
        {
            if (!isClient || !other.gameObject.CompareTag("Player")) return;

            if (other.TryGetComponent(out NetworkIdentity networkIdentity))
                if (networkIdentity == NetworkClient.localPlayer)
                    triggerUI.SetActive(false);
        }

        [Command(requiresAuthority = false)]
        void CmdTakeControl(NetworkConnectionToClient conn = null)
        {
            // someone else is already controlling this tank
            if (connectionToClient != null) return;

            // cache the regular player object
            conn.authenticationData = conn.identity.gameObject;

            // set the color to match the player
            if (conn.identity.TryGetComponent(out RandomColor randomColor))
                tankTurret.playerColor = randomColor.color;

            isControlled = true;

            // set the player object to be the tank, keep ownership of
            // the original player object to avoid ChangeOwner message.
            NetworkServer.ReplacePlayerForConnection(conn, gameObject);

            StartCoroutine(UnspawnOldPlayer((GameObject)conn.authenticationData));
        }

        IEnumerator UnspawnOldPlayer(GameObject player)
        {
            yield return new WaitForSeconds(0.1f);
            NetworkServer.UnSpawn(player);
        }

        [Command]
        void CmdReleaseControl()
        {
            // get the regular player object
            if (connectionToClient.authenticationData is GameObject player)
            {
                // Set pos and rot to match the tank, plus 3m offset to the right
                player.transform.SetPositionAndRotation(transform.position + transform.right * 3, transform.rotation);

                // clear the player object
                connectionToClient.authenticationData = null;

                // set the player object back to the player
                isControlled = false;
                tankTurret.playerColor = Color.black;
                NetworkServer.ReplacePlayerForConnection(connectionToClient, player);
            }
        }

        #region Start & Stop Callbacks

        /// <summary>
        /// This is invoked for NetworkBehaviour objects when they become active on the server.
        /// <para>This could be triggered by NetworkServer.Listen() for objects in the scene, or by NetworkServer.Spawn() for objects that are dynamically created.</para>
        /// <para>This will be called for objects on a "host" as well as for object on a dedicated server.</para>
        /// </summary>
        public override void OnStartServer() { }

        /// <summary>
        /// Invoked on the server when the object is unspawned
        /// <para>Useful for saving object data in persistent storage</para>
        /// </summary>
        public override void OnStopServer() { }

        /// <summary>
        /// Called on every NetworkBehaviour when it is activated on a client.
        /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
        /// </summary>
        public override void OnStartClient() { }

        /// <summary>
        /// This is invoked on clients when the server has caused this object to be destroyed.
        /// <para>This can be used as a hook to invoke effects or do client specific cleanup.</para>
        /// </summary>
        public override void OnStopClient() { }

        /// <summary>
        /// Called when the local player object has been set up.
        /// <para>This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.</para>
        /// </summary>
        public override void OnStartLocalPlayer() { }

        /// <summary>
        /// Called when the local player object is being stopped.
        /// <para>This happens before OnStopClient(), as it may be triggered by an ownership message from the server, or because the player object is being destroyed. This is an appropriate place to deactivate components or functionality that should only be active for the local player, such as cameras and input.</para>
        /// </summary>
        public override void OnStopLocalPlayer() { }

        /// <summary>
        /// This is invoked on behaviours that have authority, based on context and <see cref="NetworkIdentity.hasAuthority">NetworkIdentity.hasAuthority</see>.
        /// <para>This is called after <see cref="OnStartServer">OnStartServer</see> and before <see cref="OnStartClient">OnStartClient.</see></para>
        /// <para>When <see cref="NetworkIdentity.AssignClientAuthority">AssignClientAuthority</see> is called on the server, this will be called on the client that owns the object. When an object is spawned with <see cref="NetworkServer.Spawn">NetworkServer.Spawn</see> with a NetworkConnectionToClient parameter included, this will be called on the client that owns the object.</para>
        /// </summary>
        public override void OnStartAuthority()
        {
            if (triggerUI.TryGetComponent(out TextMesh textMesh))
                textMesh.text = "Press 'X' to release control";
        }

        /// <summary>
        /// This is invoked on behaviours when authority is removed.
        /// <para>When NetworkIdentity.RemoveClientAuthority is called on the server, this will be called on the client that owns the object.</para>
        /// </summary>
        public override void OnStopAuthority()
        {
            if (triggerUI.TryGetComponent(out TextMesh textMesh))
                textMesh.text = "Press 'C' to take control";
        }

        #endregion
    }
}