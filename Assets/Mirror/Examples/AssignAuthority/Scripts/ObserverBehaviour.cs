using UnityEngine;

namespace Mirror.Examples.AssignAuthority
{
    public class ObserverBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnColorChanged))]
        public Color Color;

        public MeshRenderer MeshRenderer;

        void OnColorChanged(Color _, Color newColor) => MeshRenderer.material.color = newColor;
    }
}
