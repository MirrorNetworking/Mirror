using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Mirror.Examples.AssignAuthority
{
    [AddComponentMenu("")]
    public class OwnerBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnSecretCodeChanged))]
        int secretCode;
        public float NewCodeInterval = 5;
        float newCodeElapsed;

        public TextMesh CodeDisplay;

        void Awake() =>
            // Hide the display to start with
            SetDisplay(false);

        public override void OnStartAuthority() => SetDisplay(true);

        public override void OnStopAuthority() =>
            // Hide the display when we don't have authority anymore
            SetDisplay(false);

        public override void OnStartServer() => NewCode();

        public override void OnStartClient()
        {
            // host mode: hide display
            if (isServer) SetDisplay(false);
        }

        void Update()
        {
            newCodeElapsed += Time.deltaTime;
            if (newCodeElapsed >= NewCodeInterval)
            {
                NewCode();
                newCodeElapsed = 0;
            }
        }

        protected override void OnValidate() => syncMode = SyncMode.Owner;

        [Server]
        public void NewCode() => secretCode = Random.Range(0, 9999);

        void SetDisplay(bool show) =>
            CodeDisplay.gameObject.SetActive(show);

        void OnSecretCodeChanged(int _, int newCode)
        {
            CodeDisplay.text = newCode.ToString("0000");
        }
    }
}
