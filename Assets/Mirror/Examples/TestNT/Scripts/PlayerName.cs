using System.Collections.Generic;
using UnityEngine;
using Mirror;
using TMPro;

namespace TestNT
{
    public class PlayerName : NetworkBehaviour
    {
        internal static readonly Dictionary<NetworkConnectionToClient, string> connNames = new Dictionary<NetworkConnectionToClient, string>();
        internal static readonly HashSet<string> playerNames = new HashSet<string>();

        Transform mainCamTransform;

        [Header("Components")]
        public TextMeshPro nameText;

        [Header("SyncVars")]
        [SyncVar(hook = nameof(OnNameChanged))]
        public string playerName;

        void OnNameChanged(string _, string newValue)
        {
            nameText.text = newValue;
            gameObject.name = newValue;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            this.enabled = false;
        }

        public override void OnStartClient()
        {
            mainCamTransform = Camera.main.transform;
            this.enabled = true;
        }

        public override void OnStopClient()
        {
            this.enabled = false;
        }

        void LateUpdate()
        {
            nameText.transform.forward = mainCamTransform.forward;
        }
    }
}
