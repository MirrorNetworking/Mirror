using UnityEngine;

namespace Mirror.Examples.AssignAuthority
{
    [AddComponentMenu("")]
    public class ObserverBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnColorChanged))]
        public Color Color;

        public MeshRenderer MeshRenderer;

        void OnColorChanged(Color _, Color newColor) => MeshRenderer.material.color = newColor;
    }
}
