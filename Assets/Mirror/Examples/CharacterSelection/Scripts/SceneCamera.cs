using UnityEngine;

namespace Mirror.Examples.CharacterSelection
{
    public class SceneCamera : NetworkBehaviour
    {
        [Header("Components")]
        [SerializeField] CharacterSelection characterSelection;
        [SerializeField] Transform cameraTarget;

        [Header("Diagnostics")]
        [ReadOnly, SerializeField] SceneReferencer sceneReferencer;
        [ReadOnly, SerializeField] Transform cameraObj;

        protected override void OnValidate()
        {
            base.OnValidate();
            Reset();
        }

        void Reset()
        {
            characterSelection = GetComponent<CharacterSelection>();
            cameraTarget = transform.Find("CameraTarget");
            this.enabled = false;
        }

        public override void OnStartAuthority()
        {
#if UNITY_2022_2_OR_NEWER
            sceneReferencer = GameObject.FindAnyObjectByType<SceneReferencer>();
#else
            // Deprecated in Unity 2023.1
            sceneReferencer = GameObject.FindObjectOfType<SceneReferencer>();
#endif

            cameraObj = sceneReferencer.cameraObject.transform;

            this.enabled = true;
        }

        public override void OnStopAuthority()
        {
            this.enabled = false;
        }

        void Update()
        {
            if (!Application.isFocused)
                return;

            if (cameraObj && characterSelection)
                characterSelection.floatingInfo.forward = cameraObj.transform.forward;

            if (cameraObj && cameraTarget)
                cameraObj.SetPositionAndRotation(cameraTarget.position, cameraTarget.rotation);
        }
    }
}
